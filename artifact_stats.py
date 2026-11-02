"""Artifact statistics from evaluation CSV files (Evaluation.cs writes them).

    python artifact_stats.py evaluation/a.csv [evaluation/b.csv ...]

Per artifact: how often it was kept when offered at the start, how long the games were with it in play
(compared with games without it), how often it was taken during the game and how often it ended in a vault
(and whether that vault's owner won).
"""
import csv
import sys

NAMES = ["Wagonetka", "Boskie Oko", "Zaginiony dokument", "Przekleta statuetka", "Buty Hermesa", "Bezdenna sakiewka",
         "Poltergeist", "Rekawice Atlasa", "Lepkie palce", "Prometejskie wiazanie", "Artefakt 11", "Artefakt 12"]


def pairs(text):
    result = {}
    for part in filter(None, text.split(";")):
        number, value = part.split(":")
        result[int(number)] = value
    return result


def main(paths):
    games = []
    for path in paths:
        with open(path, newline="") as f:
            games += [g for g in csv.DictReader(f) if g.get("artifact_end")]
    if not games:
        print("no games with artifact data (the CSV must come from a build with artifact statistics)")
        return

    seats = 4
    offered = [0] * 13
    kept = [0] * 13
    rounds_with = [[] for _ in range(13)]
    taken_total = [0] * 13
    taken_games = [0] * 13
    in_vault = [0] * 13
    owner_won = [0] * 13
    all_rounds = [int(g["rounds"]) for g in games]
    for g in games:
        rounds = int(g["rounds"])
        scores = [int(g[f"score{i}"]) for i in range(seats)]
        best = max(scores)
        for i in range(seats):
            for number in filter(None, g[f"offered{i}"].split(";")):
                offered[int(number)] += 1
            if g[f"kept{i}"]:
                kept[int(g[f"kept{i}"])] += 1
        for number, count in pairs(g["artifact_takes"]).items():
            rounds_with[number].append(rounds)
            taken_total[number] += int(count)
            taken_games[number] += int(count) > 0
        for number, place in pairs(g["artifact_end"]).items():
            if place.startswith("v"):
                in_vault[number] += 1
                owner_won[number] += scores[int(place[1:])] == best

    average = sum(all_rounds) / len(all_rounds)
    print(f"{len(games)} games, {average:.1f} rounds on average")
    print(f"{'artifact':24} {'kept when offered':>17} {'in games':>8} {'rounds with':>11} {'without':>7} "
          f"{'taken in game':>13} {'takes/game':>10} {'ends in vault':>13} {'owner wins':>10}")
    for number in range(1, 13):
        n = len(rounds_with[number])
        if offered[number] == 0 and n == 0:
            continue
        with_avg = sum(rounds_with[number]) / n if n else float("nan")
        without = len(games) - n
        without_avg = (sum(all_rounds) - sum(rounds_with[number])) / without if without else float("nan")
        kept_rate = kept[number] / offered[number] if offered[number] else float("nan")
        print(f"{number:2}. {NAMES[number - 1]:20} {kept_rate:16.0%} {n:8} {with_avg:11.1f} {without_avg:7.1f} "
              f"{taken_games[number] / max(n, 1):13.0%} {taken_total[number] / max(n, 1):10.2f} "
              f"{in_vault[number] / max(n, 1):13.0%} {owner_won[number] / max(in_vault[number], 1):10.0%}")
    print("kept when offered: fair share is 33% with 3 offered; owner wins: the player with it in the vault had the best score")


if __name__ == "__main__":
    main(sys.argv[1:])
