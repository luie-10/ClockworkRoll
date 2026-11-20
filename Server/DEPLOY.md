# 무한 모드 랭킹 서버 공개하기

서버는 `ranking-server.js` 하나이고 외부 라이브러리를 쓰지 않습니다. 이 폴더의 `Dockerfile`로 어디서든 띄울 수 있습니다.

## Cloudflare Workers (추천: 무료, 카드 없음, HTTPS 자동, PC를 켜 둘 필요 없음)

`Server/cloudflare/` 폴더가 같은 API를 Workers와 D1(데이터베이스)로 구현한 버전입니다. 게임은 주소만 바꾸면 됩니다.

1. https://dash.cloudflare.com/sign-up 에서 무료로 가입합니다 (이메일 인증만 필요).
2. `Server/cloudflare` 폴더에서 차례로 실행합니다.
   ```
   npm install
   npx wrangler login                                  # 브라우저가 열리면 Allow
   npx wrangler d1 create clockworkroll-ranking        # 나온 database_id를 wrangler.toml에 넣는다
   npx wrangler d1 execute clockworkroll-ranking --remote --file=schema.sql
   npx wrangler deploy
   ```
3. `https://clockworkroll-ranking.<계정이름>.workers.dev` 주소가 나옵니다. `주소/api/health`에서 `{"ok":true}`가 보이면 성공입니다.
4. 게임의 **GameManager → Ranking Server Url**에 그 주소를 넣습니다.

무료 한도는 하루 요청 10만 번입니다. 기록 보기: `npx wrangler d1 execute clockworkroll-ranking --remote --command "SELECT * FROM players ORDER BY loop DESC"`

## Render (한국에서 가까운 싱가포르, HTTPS 자동)

1. https://render.com 에 가입하고 GitHub 계정을 연결합니다.
2. 이 저장소에 `Server/` 폴더와 루트의 `render.yaml`이 올라가 있어야 합니다 (커밋하고 push).
3. Render 대시보드에서 **New → Blueprint**를 누르고 `luie-10/ClockworkRoll` 저장소를 고른 뒤 **Apply**를 누릅니다.
   - `render.yaml`대로 `clockworkroll-ranking` 웹 서비스가 만들어지고, 1GB 디스크가 붙습니다.
   - 기록을 디스크에 남기려면 **Starter 이상 플랜(유료)**이 필요합니다. Free 플랜은 디스크가 없어서 서버가 잠들거나 다시 배포될 때 기록이 사라집니다.
4. 배포가 끝나면 `https://clockworkroll-ranking-xxxx.onrender.com` 같은 주소가 나옵니다.
   - `주소/api/health`를 열었을 때 `{"ok":true}`가 보이면 성공입니다.
5. 게임에서 **GameManager → Ranking Server Url**에 그 주소를 넣습니다 (`https://`로 시작, 끝에 `/` 없이).

## Fly.io (도쿄 지역)

카드 등록이 필요합니다. `Server` 폴더에서 다음을 차례로 실행합니다.

```
fly launch --copy-config --no-deploy
fly volumes create ranking_data --size 1 --region nrt
fly deploy
```

주소는 `https://clockworkroll-ranking.fly.dev`입니다.

## 내 서버 (VPS)

도메인을 서버 IP에 연결한 뒤 `RANK_DOMAIN=rank.내도메인 docker compose up -d`를 실행하면, Caddy가 HTTPS 인증서를 자동으로 받습니다.

## 위조 막기 설정

| 환경 변수 | 기본값 | 뜻 |
|---|---|---|
| `MIN_LOOP_SECONDS` | `60` | 한 회차(지도와 보스)를 이보다 빨리 넘기면 거부합니다. 실제로 가장 빨리 깬 기록보다 조금 낮게 잡으세요. |
| `RANK_SECRET` | (게임에 든 키) | 바꾸려면 `Assets/Scripts/Core/Ranking.cs`의 `keyA`/`keyB`도 새 키에 맞게 바꿔야 합니다. 둘이 다르면 모든 기록이 거부됩니다. |
| `TRUST_PROXY` | Docker에서 `1` | HTTPS 프록시 뒤에서 실제 접속 주소로 횟수를 제한합니다. |

게임 실행 파일 안에 서명 키가 들어 있으므로 100% 막을 수는 없습니다. 그래서 다섯 가지 장치를 겹쳐 둡니다.

- 서명
- 회차 순서 (건너뛰기 불가)
- 회차당 최소 시간
- 일회용 nonce (같은 요청을 다시 보내도 안 먹힘)
- 접속 주소별 횟수 제한

키를 뽑아낸 사람도 실제로 플레이하는 속도보다 빨리 기록을 올릴 수는 없습니다. 이상한 기록은 `data/endless.json`에서 그 줄을 지우고 서버를 다시 켜면 됩니다.
