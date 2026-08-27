#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MODELS_DIR="$SCRIPT_DIR/models"
mkdir -p "$MODELS_DIR"

DEFAULT_MODEL="$MODELS_DIR/rmbg-1.4-fp16.onnx"

if [ -f "$DEFAULT_MODEL" ]; then
  echo "Model already present at $DEFAULT_MODEL ($(du -h "$DEFAULT_MODEL" | cut -f1))"
  exit 0
fi

echo "Downloading briaai/RMBG-1.4 (FP16, ~84 MB)..."
curl -L --fail --show-error -o "$DEFAULT_MODEL" \
  "https://huggingface.co/briaai/RMBG-1.4/resolve/main/onnx/model_fp16.onnx"

echo "Done. Model at $DEFAULT_MODEL ($(du -h "$DEFAULT_MODEL" | cut -f1))"
echo ""
echo "Alternatives (bigger or smaller):"
echo "  briaai/RMBG-2.0  FP16 ~125 MB: https://huggingface.co/briaai/RMBG-2.0/resolve/main/onnx/model_fp16.onnx"
echo "  briaai/RMBG-1.4  FP32 ~170 MB: https://huggingface.co/briaai/RMBG-1.4/resolve/main/onnx/model.onnx"
echo "  briaai/RMBG-1.4  int8 ~45 MB: https://huggingface.co/briaai/RMBG-1.4/resolve/main/onnx/model_quantized.onnx"
echo ""
echo "Override the path in appsettings.json: \"Optimizer\": { \"ModelPath\": \"...\" }"
