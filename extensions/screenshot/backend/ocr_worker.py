"""Offline Chinese/English OCR using bundled ONNX models; no network calls."""
import json
import os
import sys
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "site"))
os.environ["OMP_NUM_THREADS"] = "2"


def main():
    image, result_path = sys.argv[1:3]
    try:
        from rapidocr_onnxruntime import RapidOCR
        engine = RapidOCR(intra_op_num_threads=2, inter_op_num_threads=1)
        result, elapsed = engine(image)
        # The Chinese model often drops word spaces in English lines. Reread
        # Latin-only lines with the dedicated English recognizer, keeping the
        # Chinese model's results for Chinese or mixed-language lines.
        latin = [item for item in result or [] if re.search("[A-Za-z]", item[1]) and not re.search("[\u4e00-\u9fff]", item[1])]
        english_model = ROOT / "models" / "en_ocr" / "rec.onnx"
        if latin and english_model.is_file():
            import numpy as np
            english = RapidOCR(rec_model_path=str(english_model), intra_op_num_threads=2, inter_op_num_threads=1)
            english_result, _ = english(image)
            for item in latin:
                center = np.mean(item[0], axis=0)
                candidates = sorted(english_result or [], key=lambda other: float(np.linalg.norm(np.mean(other[0], axis=0) - center)))
                if candidates:
                    candidate = candidates[0]
                    height = float(np.linalg.norm(np.asarray(item[0][0]) - np.asarray(item[0][3])))
                    distance = float(np.linalg.norm(np.mean(candidate[0], axis=0) - center))
                    if distance < max(10, height) and candidate[2] > 0.70:
                        item[1] = candidate[1]
        lines = [{"text": item[1], "box": [[float(p[0]), float(p[1])] for p in item[0]],
                  "score": float(item[2])} for item in result or []]
        response = {"ok": True, "text": "\n".join(item[1] for item in result or []), "lines": lines,
                    "language": "chinese-english", "engine": "rapidocr-onnx"}
        code = 0
    except Exception as exc:
        response, code = {"ok": False, "error": str(exc)}, 1
    Path(result_path).write_text(json.dumps(response, ensure_ascii=False), encoding="utf-8")
    return code


if __name__ == "__main__":
    raise SystemExit(main())
