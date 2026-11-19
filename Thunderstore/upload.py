"""Uploads a package zip to Thunderstore through its API, the way Thunderstore's own CLI (tcli) does,
and prints the server's full answer at any step that fails (the website's upload page often doesn't).

    python Thunderstore/upload.py Thunderstore/bin/SoapieScripts-Doom_Together-0.1.0.zip
    python Thunderstore/upload.py <zip> --no-submit     uploads the file but doesn't publish it

The token is a Thunderstore service account token for the team (Teams > the team > Service Accounts).
It's read from the THUNDERSTORE_TOKEN environment variable, or from a file named .thunderstore-token
in your user folder. Never put it in the repo.

Steps (from tcli's ApiHelper.cs / PublishCommand.cs):
  1. POST api/experimental/usermedia/initiate-upload/  {filename, file_size_bytes} -> 201, upload_urls
  2. PUT each part to its upload URL (Content-MD5), keeping the ETag
  3. POST api/experimental/usermedia/<uuid>/finish-upload/  {parts: [{ETag, PartNumber}]}
  4. POST api/experimental/submission/submit/  {author_name, communities, community_categories,
     categories, has_nsfw_content, upload_uuid} -> 200, package_version
"""

import base64
import hashlib
import json
import os
import pathlib
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import zipfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
API = "https://thunderstore.io/api/experimental/"
COMMUNITY = "on-together"
CATEGORIES = ["mods"]
AGENT = "OnTogetherMods-upload"
# Big uploads sometimes get cut off partway, so each part is tried this many times.
ATTEMPTS = 4


class UploadError(Exception):
    pass


def token():
    value = os.environ.get("THUNDERSTORE_TOKEN", "").strip()
    if not value:
        path = pathlib.Path.home() / ".thunderstore-token"
        if path.is_file():
            value = path.read_text(encoding="utf-8").strip()
    if not value:
        raise UploadError("No token: set THUNDERSTORE_TOKEN, or put it in .thunderstore-token in your user folder.")
    return value


def call(method, url, step, auth=None, body=None, data=None, headers=None, expect=(200,)):
    headers = dict(headers or {})
    headers["User-Agent"] = AGENT
    if auth:
        headers["Authorization"] = f"Bearer {auth}"
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    request = urllib.request.Request(url, data=data, method=method, headers=headers)
    try:
        with urllib.request.urlopen(request, timeout=600) as response:
            status, text, reply_headers = response.status, response.read(), response.headers
    except urllib.error.HTTPError as error:
        status, text, reply_headers = error.code, error.read(), error.headers
    except urllib.error.URLError as error:
        raise UploadError(f"{step}: no answer from the server ({error.reason})")
    if status not in expect:
        raise UploadError(f"{step}: HTTP {status}\n{text.decode('utf-8', 'replace')}")
    return text, reply_headers


def upload(zip_path, submit):
    zip_path = pathlib.Path(zip_path).resolve()
    data = zip_path.read_bytes()
    with zipfile.ZipFile(zip_path) as package:
        manifest = json.loads(package.read("manifest.json").decode("utf-8-sig"))
    team = json.loads((ROOT / "Thunderstore" / "mods.json").read_text(encoding="utf-8"))["team"]
    auth = token()
    print(f"{team}-{manifest['name']}-{manifest['version_number']} ({len(data) // 1024} KB)")

    text, _ = call("POST", API + "usermedia/initiate-upload/", "Starting the upload", auth,
                   {"filename": zip_path.name, "file_size_bytes": len(data)}, expect=(201,))
    started = json.loads(text)
    uuid = started["user_media"]["uuid"]
    parts = started["upload_urls"]
    print(f"  upload {uuid}: {len(parts)} part(s)")

    try:
        done = []
        for part in parts:
            chunk = data[part["offset"]:part["offset"] + part["length"]]
            md5 = base64.b64encode(hashlib.md5(chunk).digest()).decode("ascii")
            print(f"  part {part['part_number']} goes to {urllib.parse.urlsplit(part['url']).netloc}")
            for attempt in range(1, ATTEMPTS + 1):
                started_at = time.time()
                try:
                    _, reply = call("PUT", part["url"], f"Uploading part {part['part_number']}", data=chunk,
                                    headers={"Content-MD5": md5}, expect=(200,))
                    break
                except UploadError as error:
                    print(f"  try {attempt} failed after {time.time() - started_at:.1f} s: {error}")
                    if attempt == ATTEMPTS:
                        raise
                    time.sleep(5)
            etag = reply.get("ETag")
            if not etag:
                raise UploadError(f"Uploading part {part['part_number']}: no ETag in the answer")
            done.append({"ETag": etag, "PartNumber": part["part_number"]})
            print(f"  part {part['part_number']} sent")
        call("POST", API + f"usermedia/{uuid}/finish-upload/", "Finishing the upload", auth, {"parts": done})
        print("  upload finished")
    except UploadError:
        try:
            call("POST", API + f"usermedia/{uuid}/abort-upload/", "Cancelling the upload", auth)
        except UploadError:
            pass
        raise

    if not submit:
        print("Uploaded but not submitted (--no-submit), so nothing was published.")
        return

    text, _ = call("POST", API + "submission/submit/", "Submitting the package", auth, {
        "author_name": team,
        "communities": [COMMUNITY],
        "community_categories": {COMMUNITY: CATEGORIES},
        "categories": [],
        "has_nsfw_content": False,
        "upload_uuid": uuid,
    })
    version = json.loads(text).get("package_version") or {}
    print(f"Published {version.get('full_name', '?')}: {version.get('download_url', '?')}")


def main(args):
    submit = "--no-submit" not in args
    files = [a for a in args if not a.startswith("--")]
    if len(files) != 1:
        print(__doc__)
        return 2
    try:
        upload(files[0], submit)
    except UploadError as error:
        print(f"FAILED. {error}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
