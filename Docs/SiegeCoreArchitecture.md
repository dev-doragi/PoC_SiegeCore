# SiegeCore 전투 기반

## 책임과 상태

Framework는 앱·씬 수명, 입력, 시간, 이벤트와 풀을 담당한다. SiegeCore의 전투 규칙은 Framework에 넣지 않는다.
대포는 독립된 운반 대상이고, 성문·생산시설은 독립된 고정 기물이다. 운반 가능한 유닛은 Rat 조합으로 만든다.

| 구성 | 책임 |
| --- | --- |
| `RatAgent` | 생성 완료, 진영·공격 진영, 체력·제압, 행동 허용 조건, 게임 상태 전환 |
| `CarryableObject` | 운반 인터페이스, 부모 부착·해제, Catch 이동 트윈 |
| `RatFlightMotion` | 물리 상태, 지상 footprint, 투척·빠따·대포 궤적, 바운스·벽 귀환·착지 |
| `RatCollisionFusion` | 선택 기능. 충돌 합성 조건·잠금·생성 실패 복구·비행 계승 |
| `RatPresenter` | Definition 시각 데이터를 이용한 스프라이트·진영 색·그림자·가상 높이·스쿼시·사망 표현 |
| `RatGroundBehaviour` | 선택 기능. 개체별 지상 행동의 초기화·상태 변경·이동 표현 정보 |
| `RatGroundAI` | 기본 지상 행동. 배회·적 지시·블로킹·침투·표적 선정 |
| `Projectile` | 대포 명중·상쇄·만료 판정과 중복 종료 방지 |

게임 상태의 변경자는 `RatAgent`다. `RatFlightMotion.State`는 물리 진행 상태이며 게임 규칙의 별도 소유자가 아니다.
플레이어는 타격·Catch·운반을 요청하고, 대포는 장전·발사를 요청한다. 허용 조건을 통과한 요청만 소유권을 바꾼다.
물리 착지는 `Settled`로 알려 Rat의 지상 복귀 또는 Projectile 만료 처리를 수행한다.
예전 Carryable 상태 이벤트를 다시 해석하는 경로와 이벤트 억제 플래그는 사용하지 않는다.

## 새 Rat 추가

1. 기존 Rat 프리팹을 Variant로 만든다. `RatAgent`, `CarryableObject`, `RatFlightMotion`, `RatPresenter`, Rigidbody2D와 Collider2D를 유지한다.
2. `RatDefinition`을 만들고 종류·등급·지상 수치·탄환 수치·효과를 설정한다. 프리팹의 RatAgent에 같은 Definition을 연결한다.
3. 지상 행동이 필요하면 `RatGroundAI`를 사용한다. 새 지상 행동은 `RatGroundBehaviour`를 구현하며 한 Rat에는 하나만 둔다. 이동하지 않는 기물은 지상 행동을 생략할 수 있다.
4. 대포 탄환으로 쓰려면 `Projectile`을 붙인다. 발사·상쇄·Siege 명중·만료 효과는 `ProjectileAbility`, 지상 사망 효과는 `GroundDeathAbility` 에셋으로 연결한다.
5. 충돌 합성을 지원하면 `RatCollisionFusion`과 기존 `RatMergeResolver`를 연결한다. 머리 위 합성은 플레이어의 `RatStacking`이 같은 Resolver를 사용한다.
6. PoolDefinition을 만들고 `RatCatalog`에 Definition과 Pool을 등록한다. 생산 대상 Rank1은 해당 `RatLoadoutDefinition`에 넣는다.
7. `SiegeCore/Validate Rat Loadouts`, `SiegeCore/Validate Core Rules`를 실행한다.

공유 ScriptableObject에는 설정만 저장한다. 실행 중 타이머·표적·잠금·체력은 개체별 컴포넌트가 가진다.
기존 효과 조합에는 플레이어·대포·생산 코드의 병종별 분기를 추가하지 않는다.
새로운 적 배치 전략은 현재 `EnemyRatDirector`와 기본 `RatGroundAI`의 전용 지시 기능에서 확장한다.

## 요청과 수명

- 생성은 `RatFactory.Spawn…`을 사용한다. 풀 활성화만으로 Rat이 검색 대상에 등록되지 않는다. 초기화 완료 후 `IsSpawned`와 `Active`에 반영된다.
- Catch·부착·투척은 `TryCatch`, `TryAttachToCarrySlot`, `TryThrow`를 사용한다. 물리 컴포넌트의 내부 함수를 직접 호출하지 않는다.
- 자연 비행 Rat의 장전은 `Cannon.TryLoad`, 적 지시의 대기 Rat 장전은 `RatAgent.TryLoadIntoCannon`을 사용한다. 가득 찬 대포는 임시 비행·부모 변경 없이 거절한다.
- 발사는 `RatAgent.LaunchFromCannon`의 성공 여부를 사용한다. 원래 `Faction`은 유지하고 `AttackSide`와 발사 슬롯만 기록한다.
- 전투 요청은 직접 호출한다. 상태·사망·반환 결과는 개체 이벤트로, Siege 파괴처럼 여러 시스템이 사용하는 결과는 기존 EventBus로 알린다.
- 종료는 `Release`로 소유 풀에 반환한다. `Released`에서 운반·탄창 참조를 해제한다. 물리·트윈·블로킹·지상 지시는 재사용 전에 초기화한다.
- 풀 반환·씬 종료는 사망이 아니다. 지상 사망 효과나 탄환 상쇄 효과를 실행하지 않는다.

피해는 `IDamageable.TakeDamage(DamageData)`로 전달한다. 표적 선정은 공격자 책임이다.
중립 폭탄의 Rat 전용 대상, 지상전의 성문·시설 우선순위, Siege만 승패 HP를 가지는 규칙은 유지한다.

## 설정 이전과 검증

`SiegeCore/Migrate Rat Composition`은 기존 Rat 프리팹과 PoC 씬의 설정을 새 컴포넌트로 복사한다.
Variant 상속 순서로 처리하고, 복사한 각 값·참조를 비교한 뒤 버전을 기록한다. 재실행은 완료된 대상을 건너뛴다.
변경 전 프리팹은 `Library/RatCompositionBackup`에 보관한다. MainScene은 수정하지 않는다.

`CarryableObject.Legacy.cs`는 미이전 에셋의 직렬화 값을 읽기 위한 Editor 전용 데이터다.
런타임 동작은 이 값을 읽지 않으며 플레이어 빌드에도 포함되지 않는다. 새 설정은 기능을 소유한 컴포넌트에서 조정한다.
새로 만든 프리팹에 이전 도구를 적용하지 않는다. 기존 프로젝트 업그레이드용 메뉴다.

`Assets/99.Test/RatComposition/Rat_CompositionProbe.prefab`은 지상 AI와 충돌 합성 없이 기존 Bomber 발사 효과를 조합한 검증용 프리팹이다.
Core Rules 검증은 이 프리팹의 생성·장전·발사·재사용, 실패 요청의 상태 보존, 풀 반환 시 소유권 해제까지 함께 검사한다.
기존 합성·분열·침투·생산·승패 회귀 검증도 유지한다.

Popup/Catch 타이밍, 벽 귀환, 연쇄 합성 속도, 고속 충돌과 시각적 조작감은 Play Mode에서 별도로 확인한다.
