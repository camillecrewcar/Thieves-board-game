#!/bin/sh
# Test games of a trained model, see Assets/Evaluation.cs and evaluate_games.py.
#   ./run_evaluation.sh <name> <games> <opponents 1-3> <opponent: random | path to .onnx> [modelA .onnx]
# Results go to evaluation/<name>.csv; without modelA the model in Assets/Resources/Brains is tested.
cd "$(dirname "$0")"
mkdir -p evaluation
name=$1; games=$2; opponents=$3; opponent=$4; modelA=$5
set -- -batchmode -nographics -eval "$games" -opponents "$opponents" -opponent "$opponent" \
    -evalOut "evaluation/$name.csv" -areas 4 -maxRounds 200 -logFile "evaluation/$name.log"
[ -n "$modelA" ] && set -- "$@" -modelA "$modelA"
"Build/Thieves(board Game).exe" "$@"
python evaluate_games.py "evaluation/$name.csv"
