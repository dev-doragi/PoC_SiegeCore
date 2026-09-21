# Siege Core PoC

## 실행

Unity 6000.3.9f1에서 `Assets/00.Scenes/PoC_SiegeCore.unity`를 열고 Play.
빌드의 첫 씬도 이 씬이며 기존 Resources/AppRoot와 SceneContext가 초기화한다.
재시작은 Play Mode를 종료한 뒤 다시 시작한다.

## 조작

- WASD 이동, 마우스 조준.
- 좌클릭을 눌렀다 놓으면 빠따 공격. 최소 차지 이전 해제는 취소된다.
- 일반 공격은 Popup, 풀차지는 빠른 발사. Peak 이후 내려오는 Rat을 플레이어 위치에서 자동 Catch한다.
- 머리 위 최대 3개 운반. 우클릭은 가장 위 Rat을 던진다.
- 안전 구역에서 E를 유지하면 머리 위 아군 Rat을 순차 합성한다.
- 풀차지 Rat은 안전 구역에서 아군 Rat과 충돌해 B+B=BB, BB+B=BBB로 합성하고 속도를 이어간다.
- 풀차지가 벽에 닿으면 플레이어 방향 Return Popup으로 전환된다.
- 적 Rat은 타격 피해와 제압을 받는다. 운반 중 회복하면 전체 Drop과 플레이어 Knockback이 발생한다.

## 자원과 승리

동일한 생산 Rat을 Cannon 또는 Exit에 투입한다. Cannon은 탄환 상쇄 및 적 Siege 피해,
Ground는 Entrance 파괴 후 적 기지의 Rat·생산시설 공격에 사용한다.
지상전은 Siege HP를 직접 깎지 않는다. 적 Siege HP 0이면 승리, 아군 Siege HP 0이면 패배한다.

진영별 생산시설 2개, 각각 HP 30. 생산 비율은 100% → 62.5% → 25%이며 한 판 동안 복구되지 않는다.
아군 기본 생산 간격 5초, 적 8초. 최저 생산은 각각 20초, 32초이며 기존 출구에서 계속 나온다.
시설 파괴는 진행 중인 생산의 진행률을 유지한다.

진영별 생산 상한은 B 환산 30: B=1, BB=2, BBB=3. 운반·장전·탄환도 포함하며 사망한 Rat은 제외한다.
포획해도 원래 진영으로 계산한다. 상한은 신규 생산만 막고 합성·분열은 막지 않는다.
상한에서 생산 진행률은 초기화되며 자리가 나면 새 주기를 시작한다.

## BBB

HP 30, 지상 공격력 9, Siege 피해 30.
- Siege 명중: 피해 후 소멸, 분열하지 않는다.
- 탄환 상쇄: 원래 진영 B 3마리로 분열한다.
- 지상전 사망: B 3마리로 분열하며 침투 상태를 이어받는다. 지상 전투 중 타격으로 공중에 뜬 경우도 포함한다.
- 비행 만료·풀 반환·씬 정리·대기 Rat 사망은 분열하지 않는다.
포획한 적 BBB를 아군 대포로 쏘면 Siege 피해 진영은 아군이지만 분열 결과는 적이다.

## 씬과 조정

- RatDefinition: B/BB/BBB 전투 능력치.
- RatCatalog: 모든 RatDefinition과 각 오브젝트 풀의 매핑, Basic Rank1 폴백.
- RatLoadoutDefinition: 생산에 사용할 Rank1 Rat과 Copies. Dispenser별로 하나씩 연결한다.
- RatDispenser: Loadout 덱 드로우, 생산시설 참조, 기본 간격, 최소 생산 비율, B 환산 상한.
- GroundGate: 기지 영역, Exit, Entrance 연결.
- RatStructure: 진영, Entrance 여부, HP, 파괴 시 숨길 충돌체·시각 요소.
- RatEconomyHUD: 양측 Siege HP·생산 비율·B 환산 보유량 및 결과 표시.

`SiegeCore/Configure Core Loop`는 이 PoC 씬의 시설·기지 영역·HUD와 초기값을 다시 연결한다.
수동 튜닝 뒤 실행하면 해당 기본값을 재적용하므로 주의한다. Framework나 풀 정의를 수정하지 않는다.

로드아웃은 전투 시작 시 런타임 덱으로 복사된다. Draw Pile이 소진되면 Discard Pile을 셔플해 재사용하며,
생산에 실패한 Rat은 덱에서 소비하지 않는다. Loadout이 비어 있거나 유효한 Rank1 항목이 없으면
RatCatalog의 Basic Rank1 폴백을 사용한다. 합성·분열은 Loadout을 우회해 명시된 RatDefinition을 생성한다.

`SiegeCore/Migrate Rat Catalog and Loadouts`는 기존 PoolDefinition의 prefab 참조를 Unity Editor API로
재연결하고 Catalog·Basic Loadout 및 씬 참조를 생성한다. `SiegeCore/Validate Rat Loadouts`는 Catalog,
덱 순환, Basic 폴백을 검사한다.

## 검증

`SiegeCore/Validate Core Rules`는 실제 씬을 Play Mode로 열고 규칙을 검사한다.
배치 진입점은 `-batchmode -nographics -executeMethod SiegePocValidation.Run` (`-quit` 제외).
성공 로그: `SIEGE_POC_VALIDATION_OK`. 실패는 예외와 종료 코드 1로 보고한다.

자동 검증: BBB 소멸 원인·중복 분열·공중 지상전 사망·침투 상태 유지, 시설 파괴 영속성,
생산 단계·진행률·상한·재개, 침투 표적, Siege 종료 및 생산 정지.

수동 Play Mode 확인: Popup/Catch 타이밍, 벽 귀환, 연쇄 합성 속도, 3개 운반과 적 회복 Drop,
포획탄 투입, 고속 Cannon/Exit 충돌, 전체 지상 동선, HUD 가독성 및 자원 배분의 재미.
