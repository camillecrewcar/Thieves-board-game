"""Summarises the CSV files written by the game's evaluation mode ("-eval N", see Assets/Evaluation.cs).

    python evaluate_games.py eval_vs_random.csv [more.csv ...]
"""
import csv
import math
import sys


def summarise(path):
    with open(path, newline="") as f:
        games = list(csv.DictReader(f))
    if not games:
        print(f"{path}: no games")
        return
    seats = [s for s in range(4) if f"side{s}" in games[0]]
    sides = sorted({g[f"side{s}"] for g in games for s in seats})
    stats = {side: {"seats": 0, "wins": 0.0, "place": 0.0, "score": 0, "artifacts": 0, "declared": 0} for side in sides}
    rounds = hit_limit = 0
    for g in games:
        rounds += int(g["rounds"])
        hit_limit += int(g["hit_limit"])
        if g["declarer"] in stats:
            stats[g["declarer"]]["declared"] += 1
        scores = [int(g[f"score{s}"]) for s in seats]
        best = max(scores)
        winners = scores.count(best)
        for s in seats:
            st = stats[g[f"side{s}"]]
            score = scores[s]
            better = sum(1 for o in scores if o > score)
            equal = sum(1 for o in scores if o == score) - 1
            st["seats"] += 1
            # a shared first place counts as a share of a win
            st["wins"] += 1.0 / winners if score == best else 0.0
            st["place"] += better + equal / 2 + 1
            st["score"] += score
            st["artifacts"] += int(g[f"artifacts{s}"])
    n = len(games)
    print(f"{path}: {n} games, {rounds / n:.1f} rounds on average, {100 * hit_limit / n:.0f}% hit the round limit")
    fair = 1.0 / len(seats)
    names = {"A": "A (tested model)", "B": "B (other model)", "R": "R (random)"}
    for side in sides:
        st = stats[side]
        k = st["seats"]
        win = st["wins"] / k
        # 95% interval of the win rate per seat
        margin = 1.96 * math.sqrt(win * (1 - win) / k)
        print(f"  {names.get(side, side):18s} seats {k:5d}  win per seat {100 * win:5.1f}% ±{100 * margin:4.1f} (fair {100 * fair:.0f}%)"
              f"  place {st['place'] / k:.2f}  score {st['score'] / k:5.1f}  artifacts in vault {st['artifacts'] / k:.2f}"
              f"  declared {100 * st['declared'] / n:.0f}% of games")


if __name__ == "__main__":
    for p in sys.argv[1:]:
        summarise(p)
