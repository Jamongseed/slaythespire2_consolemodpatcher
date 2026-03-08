# StS2DllPatcher

Slay the Spire 2의 숨겨진 개발자 콘솔 명령 일부를 활성화하는 간단한 DLL 패처입니다.

현재 패치 대상 명령:

- `card`
- `relic`
- `gold`
- `potion`
- `upgradecard`
- `removecard`
- `heal`
- `energy`

이 패처는 `sts2.dll` 내부의 특정 콘솔 명령 클래스에 `DebugOnly = false` override를 추가하거나 수정하여, 릴리즈 빌드에서도 해당 명령이 콘솔에 표시되도록 만듭니다.
콘솔은 '`' 키 를 통해 열 수 있습니다.

## 주의

- 게임 업데이트 후 `sts2.dll` 구조가 바뀌면 패처가 동작하지 않을 수 있습니다.
- 패치 전 반드시 게임을 종료하세요.
- 멀티플레이 환경에서는 사용을 권장하지 않습니다. 사용 시 연결이 종료될 수 있습니다.
- 사용 전 원본 백업이 자동으로 `sts2.dll.bak`로 생성됩니다.

## 지원 파일 경로 예시

보통 `sts2.dll`은 아래 경로에 있습니다.

```text
C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\sts2.dll

## 사용 방법

콘솔창을 열어 각 명령어를 입력합니다.
예를 들어, 아이언 클레드의 몸통 박치기 카드를 손에 추가하려면
card BODY_SLAM hand 를 입력합니다.
덱에 추가하려면
card BODY_SLAM deck 을 입력합니다.

- `relic add vajra`
- `gold 999`
- `potion <name>`
- `upgradecard <name>`
- `removecard <name>`
- `heal 50`
- `energy 50`

## 면책

이 프로젝트는 비공식 개인 도구입니다.
게임 파일 수정으로 인해 발생하는 문제는 사용자 책임입니다.

