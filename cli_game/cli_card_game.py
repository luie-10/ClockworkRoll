#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
기억을 삼키는 무대 — 라오루풍 카드게임 CLI 체험판 (단일 파일)

실행:
    python cli_card_game.py              # 직접 플레이 (카드 번호로 선택)
    python cli_card_game.py --demo       # AI 자동 진행 관전
    python cli_card_game.py --seed 42    # 랜덤 시드 고정 (재현 테스트)
    python cli_card_game.py --no-color   # 색상 끄기

요구 사항: Python 3.8+ (표준 라이브러리만 사용 — pip 설치 불필요)
Windows:   한글이 깨지면 실행 전 `chcp 65001` 실행 또는 Windows Terminal 사용

핵심 시스템 (Unity판과 동일 규칙):
  - 기력: 카드 코스트. 턴마다 +1 회복, 공격 주사위로 합을 이기면 +1 추가
  - 타입 상성: 참격 > 관통 > 타격 > 참격 (우위 시 굴림 +2) / 회피는 공격 상대 +2
  - 동점: 양쪽 모두 무효
  - 책장 흡수(◈): 승리 시 적이 쓰던 책장을 획득
  - 침식: 흡수 책장 사용 시 확률적 자해 피해, 중복 흡수일수록 강해짐
  - 감정 단계: 공격이 적중할 때마다 게이지 +2, 10pt마다 레벨업(최대 5)
  - 스토리 → 전투 → 보상 → 다음 스토리 (3막 구성)
"""

import os
import sys
import time
import random
import unicodedata

# ═══════════════════════ 콘솔/그래픽 유틸 ═══════════════════════

IS_WIN = (os.name == "nt")
USE_COLOR = True
USE_CLEAR = True

RESET = "\033[0m"

def col(code, s):
    """ANSI 색상 적용"""
    if USE_COLOR:
        return "\033[{}m{}\033[0m".format(code, s)
    return s

def setup_console():
    if IS_WIN:
        os.system("")   # Windows 콘솔 ANSI 이스케이프 활성화
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

def vwidth(s):
    """터미널 표시 폭 (한글 등 넓은 글자 = 2)"""
    w = 0
    for ch in s:
        w += 2 if unicodedata.east_asian_width(ch) in ("W", "F") else 1
    return w

def ppad(s, width):
    return s + " " * max(0, width - vwidth(s))

def ptrunc(s, width):
    out = ""
    for ch in s:
        if vwidth(out + ch) > width:
            break
        out += ch
    return out

def hp_bar(cur, mx, width=12):
    filled = int(round(width * cur / mx)) if mx > 0 else 0
    filled = max(0, min(width, filled))
    return "[" + "█" * filled + "·" * (width - filled) + "]"

def join_boxes(box_list):
    """여러 카드/적 박스를 가로로 합친다 (높이 자동 정렬)"""
    height = max(len(b) for b in box_list)
    filler = max(len(b[0]) for b in box_list)
    normalized = []
    for b in box_list:
        b = list(b)
        while len(b) < height:
            b.append(" " * filler)
        normalized.append(b)
    return "\n".join("  ".join(row).rstrip() for row in zip(*normalized))

# ═══════════════════════ 데이터 ═══════════════════════

KIND_KOR = {
    "slash": "참격",
    "thrust": "관통",
    "blunt": "타격",
    "guard": "가드",
    "evade": "회피",
}

RARITY_COLOR = {
    "일반": "37",      # 흰색
    "고급": "36",      # 청록
    "희귀": "94",      # 밝은 파랑
    "예술": "93",      # 밝은 노랑
    "E.G.O": "95",     # 밝은 자홍
}

class Die:
    def __init__(self, kind, lo, hi):
        self.kind = kind
        self.lo = lo
        self.hi = hi

    def roll(self):
        return random.randint(self.lo, self.hi)


class Card:
    def __init__(self, name, cost, dice, rarity="일반", emo=0):
        self.name = name
        self.cost = cost
        self.dice = list(dice)
        self.rarity = rarity
        self.emo = emo            # 필요 감정 단계
        self.absorbed = False     # 적에게서 흡수한 책장인가
        self.corruption = 0       # 침식 수치
        self.src = name           # 흡수 전 원본 이름

    def label(self):
        mark = "◈" if self.absorbed else ""
        return mark + self.name


class Unit:
    def __init__(self, name, max_hp, deck, color="91"):
        self.name = name
        self.max_hp = max_hp
        self.hp = max_hp
        self.deck = list(deck)    # 적이 사용하는 책장 목록
        self.color = color
        self.counted = False      # 사망 로그 중복 방지

    @property
    def alive(self):
        return self.hp > 0

    def hit(self, dmg):
        self.hp = max(0, self.hp - dmg)


def mkcard(name, cost, kind, lo, hi, rarity="일반", emo=0):
    return Card(name, cost, [Die(kind, lo, hi)], rarity, emo)


# ── 플레이어 시작 덱 ──
def starting_deck():
    cards = []
    for _ in range(3):
        cards.append(mkcard("기본 참격", 1, "slash", 2, 6))
    for _ in range(2):
        cards.append(mkcard("기본 가드", 1, "guard", 3, 7))
    cards.append(mkcard("기본 회피", 1, "evade", 2, 6))
    cards.append(mkcard("기본 관통", 1, "thrust", 1, 5))
    for _ in range(2):
        cards.append(mkcard("기본 타격", 2, "blunt", 3, 8))
    return cards


# ── 적 스펙 (deck = 승리 시 흡수되는 책장) ──
def build_chapters():
    ch1 = {
        "title": "1막. 빛이 꺼진 무대",
        "intro": [
            ("...", "...빛이 꺼진 무대다."),
            ("안내", "기억을 삼키는 자여. 첫 번째 문을 열어라."),
        ],
        "enemies": [
            ("낡은 파수꾼", 30, "91", [
                mkcard("야생의 참격", 1, "slash", 2, 5),
                mkcard("떼어낸 가죽", 1, "guard", 1, 4),
            ]),
        ],
        "outro": [
            ("안내", "파수꾼이 무너졌다. 그의 책장이 손에 들어온다."),
            ("안내", "삼킨 기억은 언젠가 나를 물 것이다 — 그래도 삼킨다."),
        ],
        "rewards": [mkcard("감정의 칼날", 3, "slash", 6, 12, "예술", emo=2)],
    }

    ch2 = {
        "title": "2막. 부르는 목소리",
        "intro": [
            ("...", "두 번째 문. 벽 너머에서 누군가가 나를 부른다."),
            ("...", "부르는 목소리가... 어제 삼킨 기억의 목소리다."),
        ],
        "enemies": [
            ("짖는 그림자", 35, "35", [
                mkcard("무거운 타격", 2, "blunt", 3, 7),
                mkcard("빠른 몸놀림", 1, "evade", 2, 5),
            ]),
            ("녹슨 인형", 40, "36", [
                mkcard("녹아붙은 관통", 2, "thrust", 2, 5),
                mkcard("허물어진 가드", 1, "guard", 2, 5),
            ]),
        ],
        "outro": [
            ("안내", "둘 다 쓰러졌다. 손에 남은 책장은 점점 무거워진다."),
        ],
        "rewards": [mkcard("침식된 고백", 2, "thrust", 5, 10, "희귀", emo=1)],
    }

    ch3 = {
        "title": "3막. 무대의 감시자",
        "intro": [
            ("...", "마지막 문. 감시자가 기다린다."),
            ("...", "이 무대의 진짜 주인은... 누구였을까."),
        ],
        "enemies": [
            ("무대의 감시자", 70, "95", [
                mkcard("감시자의 참격", 3, "slash", 4, 9),
                mkcard("무거른 시선", 2, "blunt", 3, 7),
                mkcard("철벽의 막", 2, "guard", 3, 6),
            ]),
        ],
        "outro": [
            ("안내", "감시자가 고개를 떨궜다. 무대는 조용해졌다."),
            ("...", "...그리고 나는 문득 깨달는다. 침식은 오염이 아니라, 기억의 무게였다는 것을."),
        ],
        "rewards": [],
    }

    return [ch1, ch2, ch3]

GAME_OVER_LINES = [
    ("...", "무릎이 꺾인다. 시야가 어두워진다."),
    ("...", "── 이 기억은, 다음에 다시 삼키겠지. ──"),
]

# ═══════════════════════ 게임 본체 ═══════════════════════

CARD_W = 16        # 카드 박스 내부 폭
ENEMY_W = 20       # 적 박스 내부 폭
KI_MAX = 10
HAND_LIMIT = 7
LOG_LINES = 7

class Game:
    def __init__(self, demo=False):
        self.demo = demo
        self.chapters = build_chapters()
        self.player = Unit("나", 60, [], "97")
        self.owned = starting_deck()      # 보유 덱 (흡수/보상 반영)
        self.draw = []
        self.discard = []
        self.hand = []
        self.enemies = []
        self.ki = 0
        self.emotion_level = 0
        self.emotion_points = 0
        self.logbuf = []

    # ── 로그/입력 ──

    def log(self, msg):
        self.logbuf.append(msg)
        if len(self.logbuf) > 40:
            self.logbuf.pop(0)

    def ask(self, prompt):
        if self.demo:
            return ""
        try:
            return input(prompt)
        except EOFError:
            return "q"

    def pause(self, t=0.25):
        if not self.demo:
            time.sleep(t)

    # ── 렌더링 ──

    def render(self, extra=None):
        out = []
        if USE_CLEAR and sys.stdout.isatty():
            out.append("\033[2J\033[H")
        elif not sys.stdout.isatty():
            out.append("─" * 60)

        # 적 박스
        if self.enemies:
            boxes = []
            for e in self.enemies:
                if not e.alive:
                    boxes.append([col("90", ppad("(쓰러짐)", ENEMY_W)),
                                  col("90", ppad("", ENEMY_W)),
                                  col("90", ppad("", ENEMY_W))])
                    continue
                name = ptrunc(e.name, ENEMY_W)
                hp = "HP {}/{}".format(e.hp, e.max_hp)
                boxes.append([
                    col(e.color, ppad(name, ENEMY_W)),
                    ppad(hp, ENEMY_W),
                    col(e.color if e.hp / e.max_hp < 0.35 else "37", ppad(hp_bar(e.hp, e.max_hp), ENEMY_W)),
                ])
            out.append(join_boxes(boxes))

        # 전투 로그
        out.append(col("90", "─ 로그 " + "─" * 40))
        for line in self.logbuf[-LOG_LINES:]:
            out.append("  " + line)

        # 내 상태
        p = self.player
        status = "{} {} {}/{}   {} {}/{}   {} LV.{}({})   {} {}/버림{}/보유{}".format(
            col("97", "나"),
            col("92", hp_bar(p.hp, p.max_hp)), p.hp, p.max_hp,
            col("96", "기력"), self.ki, KI_MAX,
            col("91", "감정"), self.emotion_level, self.emotion_points,
            col("90", "덱"), len(self.draw), len(self.discard), len(self.owned))
        out.append(status)

        # 손패
        if self.hand:
            rows = [self.hand[i:i + 4] for i in range(0, len(self.hand), 4)]
            num = 0
            for row in rows:
                boxes = []
                for card in row:
                    num += 1
                    boxes.append(self.card_box(card, num))
                out.append(join_boxes(boxes))
        else:
            out.append(col("90", "(손패가 비었다)"))

        if extra:
            out.append(extra)
        print("\n".join(out), flush=True)

    def card_box(self, card, number):
        cw = CARD_W
        color = RARITY_COLOR.get(card.rarity, "37")
        costline = "기력{}".format(card.cost)
        if card.absorbed:
            costline += " 침식{}".format(card.corruption)
        if card.emo > 0:
            costline += " 감정{}".format(card.emo)

        lines = ["┌" + "─" * cw + "┐"]
        lines.append("│ " + ppad(ptrunc(card.label(), cw), cw) + "│")
        lines.append("│ " + ppad(costline, cw) + "│")
        for die in card.dice:
            dk = col("91" if die.kind in ("slash", "thrust", "blunt") else "92", KIND_KOR[die.kind])
            raw = "{} {}~{}".format(KIND_KOR[die.kind], die.lo, die.hi)
            lines.append("│ " + ppad(ptrunc(raw, cw), cw).replace(KIND_KOR[die.kind], dk, 1) + "│")
        lines.append("└" + "─" * cw + "┘")
        # 선택 번호
        lines.insert(0, col("93", "[{}]".format(number)) + " " * (cw + 2 - vwidth("[{}]".format(number))))
        # 등급색으로 테두리 칠하기
        colored = [lines[0]] + [col(color, l) for l in lines[1:]]
        return colored

    # ── 핵심 규칙 ──

    def is_attack(self, kind):
        return kind in ("slash", "thrust", "blunt")

    def beats(self, a, b):
        return (a == "slash" and b == "thrust") or \
               (a == "thrust" and b == "blunt") or \
               (a == "blunt" and b == "slash")

    def roll_die(self, mine, theirs):
        val = mine.roll()
        if theirs is None:
            return max(val, 0)
        theirs_atk = self.is_attack(theirs.kind)
        if self.is_attack(mine.kind) and theirs_atk and self.beats(mine.kind, theirs.kind):
            val += 2
            self.log(col("93", "  → {}은(는) {}에 강하다! 굴림 +2".format(KIND_KOR[mine.kind], KIND_KOR[theirs.kind])))
        if mine.kind == "evade" and theirs_atk:
            val += 2
            self.log(col("93", "  → 회피가 공격을 겨냥한다! 굴림 +2"))
        return max(val, 0)

    def add_emotion(self, pts):
        self.emotion_points += pts
        while self.emotion_level < 5 and self.emotion_points >= (self.emotion_level + 1) * 10:
            self.emotion_level += 1
            self.log(col("91", "  감정 단계 상승! LV.{}".format(self.emotion_level)))

    def apply(self, source, target, die, val):
        if self.is_attack(die.kind):
            target.hit(val)
            self.add_emotion(2)
            self.log("  {}의 {} 주사위 → {}에게 피해 {}!".format(
                source.name, KIND_KOR[die.kind], target.name, col("91", str(val))))
            if source is self.player:
                self.ki = min(self.ki + 1, KI_MAX)
            if not target.alive and not target.counted:
                target.counted = True
                self.log(col("93", "  {} 이(가) 쓰러졌다!".format(target.name)))
            if not source.alive and not source.counted:
                source.counted = True
                self.log(col("93", "  {} 이(가) 쓰러졌다!".format(source.name)))
        else:
            self.log("  {}의 {} 주사위가 상대 주사위를 막았다!".format(
                source.name, KIND_KOR[die.kind]))

    def clash(self, atk_unit, atk_card, def_unit, def_card):
        dname = def_card.label() if def_card else "없음"
        self.log("합: {}[{}] vs {}[{}]".format(atk_unit.name, atk_card.label(), def_unit.name, dname))
        a = atk_card.dice
        d = def_card.dice if def_card else []
        for i in range(max(len(a), len(d))):
            if not atk_unit.alive or not def_unit.alive:
                break
            ad = a[i] if i < len(a) else None
            dd = d[i] if i < len(d) else None
            ar = self.roll_die(ad, dd) if ad else -1
            dr = self.roll_die(dd, ad) if dd else -1
            self.log("  주사위{}: {} [{}] vs {} [{}]".format(
                i + 1, atk_unit.name,
                "{} {}".format(KIND_KOR[ad.kind], ar) if ad else "-",
                def_unit.name,
                "{} {}".format(KIND_KOR[dd.kind], dr) if dd else "-"))
            self.pause(0.3)
            if ad is None and dd is None:
                break
            if ar == dr:
                self.log("  팽팽하게 밀렸다 — 양쪽 주사위가 함께 무효화된다!")
            elif ar > dr:
                self.apply(atk_unit, def_unit, ad, ar)
            else:
                self.apply(def_unit, atk_unit, dd, dr)

    def use_card(self, card, target):
        self.ki -= card.cost
        self.hand.remove(card)
        self.discard.append(card)
        self.log("사용: [{}] (기력 -{} → {}) → 대상: {}".format(
            card.label(), card.cost, self.ki, target.name))

        # 침식 판정 (흡수 책장)
        if card.absorbed:
            chance = min(0.8, 0.2 + 0.05 * max(0, card.corruption - 1))
            if random.random() < chance:
                dmg = 2 + max(0, card.corruption - 1)
                self.log(col("95", "  기억이 침식했다! [{}]에 깃든 원한이 샌다 → 자해 피해 {}".format(card.name, dmg)))
                self.player.hit(dmg)
                if not self.player.alive:
                    return

        response = random.choice(target.deck) if target.deck else None
        self.clash(self.player, card, target, response)

    # ── 덱/손패 ──

    def refill(self):
        if not self.draw and self.discard:
            self.draw = self.discard[:]
            self.discard = []
            random.shuffle(self.draw)
        while len(self.hand) < HAND_LIMIT and self.draw:
            self.hand.append(self.draw.pop(0))

    def first_playable(self):
        for c in self.hand:
            if c.cost <= self.ki and self.has_attack_die(c):
                return c
        for c in self.hand:
            if c.cost <= self.ki:
                return c
        return None

    def has_attack_die(self, card):
        return any(self.is_attack(d.kind) for d in card.dice)

    # ── 턴 흐름 ──

    def player_turn(self):
        actions = 3
        while True:
            self.render("[번호]=책장 선택   e=턴 종료   s=상태   q=종료")
            if not self.player.alive:
                return False

            if self.demo:
                self.pause(0.3)
                if actions <= 0:
                    break
                card = self.first_playable()
                alive = [e for e in self.enemies if e.alive]
                if card is None or not alive:
                    break
                self.use_card(card, alive[0])
                actions -= 1
                if not all(e.alive for e in self.enemies) or not self.player.alive:
                    break
                continue

            cmd = self.ask("명령> ").strip().lower()
            if cmd == "e":
                break
            if cmd == "q":
                print("무대를 떠난다...")
                sys.exit(0)
            if cmd == "s":
                self.log("덱 {}장 · 버림패 {}장 · 보유 {}장 · 흡수 책장 {}종".format(
                    len(self.draw), len(self.discard), len(self.owned),
                    len([c for c in self.owned if c.absorbed])))
                continue
            if cmd.isdigit():
                idx = int(cmd) - 1
                if not (0 <= idx < len(self.hand)):
                    self.log("그 번호의 책장은 손패에 없다.")
                    continue
                card = self.hand[idx]
                if card.cost > self.ki:
                    self.log("기력이 부족하다! (필요 {} / 현재 {})".format(card.cost, self.ki))
                    continue
                if card.emo > self.emotion_level:
                    self.log("감정 단계 {} 이상이 필요한 책장이다.".format(card.emo))
                    continue
                target = self.pick_target()
                if target is None:
                    self.log("사용을 취소했다.")
                    continue
                self.use_card(card, target)
                if all(not e.alive for e in self.enemies):
                    break
            elif cmd:
                self.log("이해할 수 없는 명령이다. (번호 / e / s / q)")
        return True

    def pick_target(self):
        alive = [e for e in self.enemies if e.alive]
        if len(alive) == 1:
            return alive[0]
        self.render()
        print("대상 선택: " + "  ".join("{}. {}".format(i + 1, e.name) for i, e in enumerate(alive)) + "  (c=취소)", flush=True)
        while True:
            t = self.ask("대상> ").strip().lower()
            if self.demo:
                return alive[0]
            if t == "c":
                return None
            if t.isdigit() and 1 <= int(t) <= len(alive):
                return alive[int(t) - 1]
            print("번호를 다시 입력하세요.", flush=True)

    def enemy_turn(self):
        self.log(col("91", "── 적의 턴 ──"))
        self.render()
        for e in [e for e in self.enemies if e.alive]:
            self.pause(0.35)
            card = random.choice(e.deck) if e.deck else None
            if card is None:
                self.log("{}: 사용할 책장이 없다.".format(e.name))
                continue
            response = random.choice(self.hand) if self.hand else None
            self.clash(e, card, self.player, response)
            if not self.player.alive:
                return False
        return True

    # ── 흡수/보상 ──

    def absorb(self, enemies):
        self.log(col("95", "=== 책장 흡수 ==="))
        for e in enemies:
            for card in e.deck:
                same = None
                for c in self.owned:
                    if c.absorbed and c.src == card.name:
                        same = c
                        break
                if same is not None:
                    same.corruption += 1
                    self.log("흡수: [{}] 재흡수 — 침식이 깊어진다 ({})".format(card.name, same.corruption))
                else:
                    copy = Card(card.name, card.cost, card.dice, card.rarity, card.emo)
                    copy.absorbed = True
                    copy.corruption = 1
                    copy.src = card.name
                    self.owned.append(copy)
                    self.log("흡수: ◈ [{}] → 내 덱에 합류 (침식 1)".format(card.name))

    def grant_rewards(self, rewards):
        for spec in rewards:
            gained = Card(spec.name, spec.cost, spec.dice, spec.rarity, spec.emo)
            self.owned.append(gained)
            self.log(col("93", "보상 책장 획득: [{}] (기력 {}, 감정 {} 필요)".format(
                spec.name, spec.cost, spec.emo)))

    # ── 스토리 ──

    def say_lines(self, lines):
        for speaker, text in lines:
            self.log(col("96", "{}: {}".format(speaker, text)))
            self.render()
            if self.demo:
                time.sleep(0.4)
            else:
                self.ask("")   # Enter 대기

    # ── 전체 진행 ──

    def run(self):
        for no, chapter in enumerate(self.chapters, 1):
            self.logbuf.clear()
            self.log(col("93", "═══ {} ═══".format(chapter["title"])))
            self.say_lines(chapter["intro"])

            if not self.battle(chapter):
                self.say_lines(GAME_OVER_LINES)
                self.render()
                return "defeat"

            self.absorb(self.enemies)
            self.grant_rewards(chapter["rewards"])
            self.say_lines(chapter["outro"])
            self.pause(0.3)

        self.log(col("93", "═══ 모든 챕터 클리어! ═══"))
        self.log("침식 총량: {} — 삼킨 기억의 무게".format(
            sum(c.corruption for c in self.owned)))
        self.render()
        return "clear"

    def battle(self, chapter):
        # 적 생성
        self.enemies = []
        for name, hp, color, deck in chapter["enemies"]:
            self.enemies.append(Unit(name, hp, deck, color))

        # 전투 초기화 (기력/감정/덱 순환 리셋)
        self.ki = 3
        self.emotion_level = 0
        self.emotion_points = 0
        self.draw = self.owned[:]
        random.shuffle(self.draw)
        self.discard = []
        self.hand = []
        self.logbuf.clear()

        self.log("=== 전투 시작 — 적 {}명, 시작 기력 {} ===".format(len(self.enemies), self.ki))
        self.render()

        while True:
            self.ki = min(self.ki + 1, KI_MAX)
            self.refill()
            self.log(col("96", "── 내 턴 (기력 {}) ──".format(self.ki)))

            if not self.player_turn():
                if not self.player.alive:
                    return False
                return False   # q로 종료된 경우 — 상위에서 처리됨

            if all(not e.alive for e in self.enemies):
                self.log(col("93", "=== 무대 클리어! ==="))
                return True

            if not self.enemy_turn():
                return False

# ═══════════════════════ 진입점 ═══════════════════════

def main():
    setup_console()
    demo = False
    args = sys.argv[1:]
    if "--no-color" in args:
        globals()["USE_COLOR"] = False
    if not sys.stdout.isatty():
        globals()["USE_CLEAR"] = False
        globals()["USE_COLOR"] = False
    if "--demo" in args:
        demo = True
    if "--seed" in args:
        try:
            random.seed(int(args[args.index("--seed") + 1]))
        except (ValueError, IndexError):
            random.seed(42)

    print(col("95", "══════ 기억을 삼키는 무대 — CLI 체험판 ══════"))
    print(col("90", "핵심 시스템: 기력 / 타입 상성 / 동점 무효 / 흡수(◈) / 침식 / 감정 단계"))

    while True:
        game = Game(demo=demo)
        result = game.run()
        if demo:
            break   # 관전 모드는 1회 실행 후 종료
        again = input("\n다시 시작할까요? (r=재시작, 그 외=종료) > ").strip().lower()
        if again != "r":
            print("무대의 문이 닫힌다. ── 끝 ──")
            break

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n(강제 종료)")
