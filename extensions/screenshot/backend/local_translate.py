"""Local-only translation worker. Never downloads models or calls a network API."""
import json
import os
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "site"))
os.environ["OMP_NUM_THREADS"] = "2"


def translate(text, model_root=None):
    import ctranslate2
    import sentencepiece as spm

    model_root = Path(model_root or ROOT / "models" / "en_zh")
    if not (model_root / "model" / "model.bin").is_file():
        raise RuntimeError("离线英译中模型尚未准备好，请先运行模型准备脚本，或切换到联网翻译。")
    metadata = json.loads((model_root / "metadata.json").read_text(encoding="utf-8"))
    tokenizer = spm.SentencePieceProcessor(model_file=str(model_root / "sentencepiece.model"))
    engine = ctranslate2.Translator(str(model_root / "model"), device="cpu", compute_type="int8", inter_threads=1, intra_threads=2)
    paragraphs = []
    for paragraph in text.splitlines():
        if not paragraph.strip() or not re.search("[A-Za-z]", paragraph):
            paragraphs.append(paragraph)
            continue
        # Bound token counts for long OCR paragraphs; preserve sentence order.
        sentences = re.split(r"(?<=[.!?;])\s+", paragraph.strip())
        output = []
        for sentence in sentences:
            pieces = tokenizer.encode(sentence, out_type=str)
            chunks = [pieces[i:i + 160] for i in range(0, len(pieces), 160)]
            prefix = metadata.get("target_prefix", "")
            results = engine.translate_batch(chunks, target_prefix=[[prefix]] * len(chunks) if prefix else None,
                                             replace_unknowns=True, beam_size=4, max_decoding_length=320,
                                             length_penalty=0.2)
            for result in results:
                value = tokenizer.decode(result.hypotheses[0])
                if prefix and value.startswith(prefix):
                    value = value[len(prefix):]
                output.append(value.strip().lstrip(" ▁"))
        paragraphs.append("".join(output))
    return "\n".join(paragraphs)


def main():
    request_path, result_path = map(Path, sys.argv[1:3])
    try:
        request = json.loads(request_path.read_text(encoding="utf-8-sig"))
        if request.get("source", "en") != "en":
            raise RuntimeError("当前本地模型支持英文到简体中文；其他语言请切换联网模式。")
        response = {"ok": True, "text": translate(request.get("text", "")), "mode": "offline"}
        code = 0
    except Exception as exc:
        response, code = {"ok": False, "error": str(exc)}, 1
    result_path.write_text(json.dumps(response, ensure_ascii=False), encoding="utf-8")
    return code


if __name__ == "__main__":
    raise SystemExit(main())
