#!/usr/bin/env python3
"""Пересборка субсета Material Symbols Rounded для SaveMoney.

Скачивает статический TTF (FILL 0, wght 400, GRAD 0, opsz 24) через Google Fonts
CSS API, оставляет только глифы из списка ICONS и пишет MaterialSymbolsRounded-sub.ttf.

Зависимости: pip install fonttools brotli
Запуск:      python tools/subset-material-symbols.py
             (результат печатает готовые строки для Controls/Glyphs.cs)

Важно: Google периодически меняет codepoints глифов — после пересборки сверяйте
выведенные коды с Glyphs.cs и обновляйте константы.
"""

import re
import subprocess
import sys
import tempfile
from pathlib import Path

from fontTools.subset import Subsetter, Options
from fontTools.ttLib import TTFont

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "src" / "SaveMoney" / "Resources" / "Fonts" / "MaterialSymbolsRounded-sub.ttf"

CSS_URL = ("https://fonts.googleapis.com/css2"
           "?family=Material+Symbols+Rounded:opsz,wght,FILL,GRAD@24,400,0,0")

# Имена глифов (ligature-имена стабильны между версиями, codepoints — нет).
ICONS = [
    # Навигация и общие
    "account_balance_wallet", "add", "apps", "arrow_back", "calendar_month",
    "check", "chevron_right", "close", "delete", "donut_small", "download",
    "edit_square", "error", "info", "more_horiz", "receipt_long", "repeat",
    "savings", "schedule", "search", "share", "sync", "trending_up", "tune",
    # Категории расходов
    "shopping_cart", "restaurant", "directions_car", "home", "medication",
    "cleaning_services", "checkroom", "content_cut", "movie", "directions_run",
    "child_care", "school", "redeem", "flight", "pets", "credit_card",
    # Категории доходов и счета
    "payments", "handyman", "loyalty", "account_balance", "undo", "favorite",
    # Дополнительная палитра редактора категорий
    "sports_esports", "local_cafe", "local_pizza", "menu_book", "inventory_2",
    "label",
    # Служебные (переводы, долги, меню «Ещё»)
    "swap_horiz", "north_east", "south_west", "category", "handshake",
    "flag", "group", "settings", "warning",
]


def download(url: str, dest: Path) -> None:
    subprocess.run(["curl", "-sL", "-o", str(dest), url], check=True)


def main() -> None:
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        css_path = tmp / "css.txt"
        download(CSS_URL, css_path)
        urls = re.findall(r"url\((https://[^)]+\.ttf)\)", css_path.read_text())
        if not urls:
            sys.exit("CSS API не отдал TTF — проверьте URL/доступ к сети")
        font_path = tmp / "MSRR.ttf"
        download(urls[0], font_path)

        font = TTFont(font_path)
        cmap = font.getBestCmap()
        name2cp: dict[str, int] = {}
        for cp, name in cmap.items():
            name2cp.setdefault(name, cp)

        missing = [n for n in ICONS if n not in name2cp]
        if missing:
            sys.exit(f"В шрифте нет глифов: {missing}")

        opts = Options(layout_features=[], hinting=False, desubroutinize=True)
        subsetter = Subsetter(options=opts)
        subsetter.populate(unicodes=[name2cp[n] for n in ICONS])
        subsetter.subset(font)
        OUT.parent.mkdir(parents=True, exist_ok=True)
        font.save(OUT)

        print(f"OK: {len(ICONS)} глифов -> {OUT} ({OUT.stat().st_size} байт)")
        print("\n// Для Controls/Glyphs.cs:")
        for n in sorted(ICONS):
            print(f"{n} U+{name2cp[n]:04X}")


if __name__ == "__main__":
    main()
