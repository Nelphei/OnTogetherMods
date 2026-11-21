// Changed for Soap Box (2026-11-20): the message no longer names the file loaders, which were
// taken out of Soap Box's copy. Everything else is MoonSharp's.
using System;

namespace MoonSharp.Interpreter.Loaders
{
	/// <summary>
	/// A script loader used for platforms we cannot initialize in any better way..
	/// </summary>
	internal class InvalidScriptLoader : IScriptLoader
	{
		string m_Error;

		internal InvalidScriptLoader(string frameworkname)
		{
			m_Error = string.Format("{0} carts can't load script files.", frameworkname);
		}

		public object LoadFile(string file, Table globalContext)
		{
			throw new PlatformNotSupportedException(m_Error);
		}

		public string ResolveFileName(string filename, Table globalContext)
		{
			return filename;
		}

		public string ResolveModuleName(string modname, Table globalContext)
		{
			throw new PlatformNotSupportedException(m_Error);
		}
	}
}
