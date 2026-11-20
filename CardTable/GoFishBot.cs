using System;
using System.Collections.Generic;
using System.Linq;

namespace CardTable
{
    // A computer player's head: it remembers what the table has shown (who asked for what, who said
    // "Go fish"), but not perfectly, so it can be beaten. It only looks at its own hand and how many
    // cards the others hold, never at their cards.
    internal sealed class GoFishBot
    {
        // The chance of noticing what someone asked for, and of forgetting a thing it knew, each turn.
        // Perfect memory wins about 62% of 2-player games against random asks, this about 54%
        // (checked outside the game), so someone paying attention can beat it. With 4 players it
        // wins about 83% against three random askers.
        private const double Notice = 0.85;
        private const double Forget = 0.08;

        private readonly int _seat;
        private readonly Random _random;
        // [seat, rank]: known to hold that rank, and known not to.
        private bool[,] _has = new bool[0, 0];
        private bool[,] _hasNot = new bool[0, 0];

        public GoFishBot(int seat, Random random)
        {
            _seat = seat;
            _random = random;
        }

        public void See(FishEvent e, int seats)
        {
            if (e.Kind == FishEventKind.Deal || _has.GetLength(0) != seats)
            {
                _has = new bool[seats, Cards.King + 1];
                _hasNot = new bool[seats, Cards.King + 1];
            }
            switch (e.Kind)
            {
                case FishEventKind.Ask:
                    // Asking shows you hold the rank; handing cards over shows the other doesn't any more.
                    if (e.Seat == _seat || _random.NextDouble() < Notice)
                    {
                        _has[e.Seat, e.Rank] = true;
                        _hasNot[e.Seat, e.Rank] = false;
                    }
                    _has[e.Target, e.Rank] = false;
                    _hasNot[e.Target, e.Rank] = true;
                    break;
                case FishEventKind.Fish:
                    if (e.Rank >= 0)
                    {
                        // Drew the card they asked for, and showed it.
                        _has[e.Seat, e.Rank] = true;
                    }
                    else if (e.Count > 0)
                    {
                        ClearHasNot(e.Seat);
                    }
                    break;
                case FishEventKind.Draw:
                    ClearHasNot(e.Seat);
                    break;
                case FishEventKind.Book:
                    for (int s = 0; s < seats; s++)
                    {
                        _has[s, e.Rank] = false;
                        _hasNot[s, e.Rank] = false;
                    }
                    break;
            }
        }

        // Picks whom to ask for what. False if it can't ask anyone.
        public bool Choose(GoFish game, out int target, out int rank)
        {
            target = -1;
            rank = -1;
            if (game.Over || game.Turn != _seat || game.Hands[_seat].Count == 0 || _has.GetLength(0) != game.Seats)
            {
                return Fallback(game, out target, out rank);
            }
            // Memory fades a little each turn.
            for (int s = 0; s < game.Seats; s++)
            {
                if (s == _seat)
                {
                    continue;
                }
                for (int r = Cards.Ace; r <= Cards.King; r++)
                {
                    if (_has[s, r] && _random.NextDouble() < Forget)
                    {
                        _has[s, r] = false;
                    }
                    if (_hasNot[s, r] && _random.NextDouble() < Forget)
                    {
                        _hasNot[s, r] = false;
                    }
                }
            }

            // Without anything remembered: the rank it holds most of (a book it's close to, which
            // nobody can take back once it's down), from someone with plenty of cards. Asking for
            // the rank with the most cards still out there instead did worse outside the game.
            double best = double.MinValue;
            foreach (int r in game.Hands[_seat].Select(Cards.Rank).Distinct())
            {
                int mine = game.CountOf(_seat, r);
                foreach (int t in game.Askable(_seat))
                {
                    double score = mine + _random.NextDouble() * 0.9 + game.Hands[t].Count * 0.15;
                    if (_has[t, r])
                    {
                        score += 10;
                    }
                    if (_hasNot[t, r])
                    {
                        score -= 6;
                    }
                    if (score > best)
                    {
                        best = score;
                        target = t;
                        rank = r;
                    }
                }
            }
            return target >= 0 || Fallback(game, out target, out rank);
        }

        // Any legal ask (in case its memory is out of step with the table).
        private bool Fallback(GoFish game, out int target, out int rank)
        {
            target = -1;
            rank = -1;
            if (game.Over || game.Turn != _seat || game.Hands[_seat].Count == 0)
            {
                return false;
            }
            List<int> targets = game.Askable(_seat).ToList();
            if (targets.Count == 0)
            {
                return false;
            }
            target = targets[_random.Next(targets.Count)];
            rank = Cards.Rank(game.Hands[_seat][_random.Next(game.Hands[_seat].Count)]);
            return true;
        }

        private void ClearHasNot(int seat)
        {
            for (int r = 0; r <= Cards.King; r++)
            {
                _hasNot[seat, r] = false;
            }
        }
    }
}
