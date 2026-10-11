"""Lazy album-art thumbnail extraction via mutagen.

Reads the embedded APIC/cover frame straight out of an MP3 (this is reading
an image from an audio file, NOT parsing AudioMirror XML - explicitly
allowed by the data contract). Thumbnails are cached in gui/.cache/thumbs
keyed by a hash of the track id, so each cover is extracted once, never per
render, and only for the currently visible page.

Read-only with respect to the library: the MP3 is opened for reading only.
"""
from __future__ import annotations

import hashlib
import io
from pathlib import Path

from gui import config, tokens

THUMB_SIZE = 300  # px, plenty for 150px grid cards on hi-dpi

# Marker content for "tried and failed" so we don't re-open a bad file every page load
_NEG = b"NOART"


def _cache_path(key: str) -> Path:
    return config.THUMBS_DIR / (hashlib.md5(key.encode("utf-8")).hexdigest() + ".jpg")


def get_thumbnail(key: str, file_path: str | None, has_art: bool = True) -> Path | None:
    """Return a cached thumbnail path for the given track, extracting it on
    first request. Returns None when there is no extractable art (missing
    file, no embedded picture, undecodable image) - callers render the
    placeholder card in that case. Never raises."""
    if not has_art or not file_path:
        return None
    cached = _cache_path(key)
    try:
        if cached.exists():
            if cached.stat().st_size <= len(_NEG):
                return None
            return cached
    except OSError:
        return None

    data = _extract_image_bytes(file_path)
    thumb = _to_jpeg_thumb(data) if data else None
    try:
        config.THUMBS_DIR.mkdir(parents=True, exist_ok=True)
        cached.write_bytes(thumb if thumb else _NEG)
    except OSError:
        return None
    return cached if thumb else None


def _extract_image_bytes(file_path: str) -> bytes | None:
    """First embedded picture in the MP3, or None."""
    try:
        from mutagen.id3 import ID3
        tags = ID3(file_path)
        pics = tags.getall("APIC")
        if pics:
            return bytes(pics[0].data)
    except Exception:
        pass
    # Fallback for non-ID3 containers mutagen can still read
    try:
        from mutagen import File as MFile
        mf = MFile(file_path)
        if mf is not None and hasattr(mf, "pictures") and mf.pictures:
            return bytes(mf.pictures[0].data)
    except Exception:
        pass
    return None


def _to_jpeg_thumb(data: bytes) -> bytes | None:
    """Downscale to THUMB_SIZE JPEG. Falls back to raw bytes if Pillow can't
    decode but the browser might (rare formats)."""
    try:
        from PIL import Image
        img = Image.open(io.BytesIO(data))
        img = img.convert("RGB")
        img.thumbnail((THUMB_SIZE, THUMB_SIZE))
        out = io.BytesIO()
        img.save(out, format="JPEG", quality=82)
        return out.getvalue()
    except Exception:
        return data if data[:3] in (b"\xff\xd8\xff",) or data[:8] == b"\x89PNG\r\n\x1a\n" else None


PLACEHOLDER_GRADIENTS = [
    (tokens.ACCENT4, tokens.HUE_7A2D2D), (tokens.ACCENT3, tokens.HUE_8A5A10), (tokens.HUE_5C6270, tokens.SURFACE_23262E),
    (tokens.ACCENT5, tokens.HUE_5A3A8A), (tokens.ACCENT, tokens.SURFACE_243A7A), (tokens.ACCENT2, tokens.HUE_1F5C48),
    (tokens.HUE_E2905B, tokens.HUE_7A3F1F), (tokens.HUE_5BB8FF, tokens.HUE_1F4A7A), (tokens.HUE_8A7FF0, tokens.SURFACE_3A2F8A),
    (tokens.HUE_E26DB8, tokens.HUE_7A2D5A), (tokens.HUE_D1C37F, tokens.HUE_5C521F), (tokens.HUE_E2585B, tokens.SURFACE_7A1F22),
]


def placeholder_style(seed: str) -> str:
    """Deterministic varied gradient for a track without extractable art,
    so the grid still reads as a wall rather than a uniform placeholder."""
    idx = int(hashlib.md5(seed.encode("utf-8")).hexdigest(), 16) % len(PLACEHOLDER_GRADIENTS)
    a, b = PLACEHOLDER_GRADIENTS[idx]
    return f"background:linear-gradient(135deg,{a},{b});"


def initials(title: str, album: str = "") -> str:
    src = (album or title or "?").strip()
    words = [w for w in src.split() if w and w[0].isalnum()]
    if len(words) >= 2:
        return (words[0][0] + words[1][0]).upper()
    return src[:2].upper() if src else "?"
