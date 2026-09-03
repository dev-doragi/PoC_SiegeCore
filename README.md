## **0. 핵심 지침 (Core Principles)**
* **MainScene 보호**: 핵심 시스템 구현 단계이므로 `MainScene`은 수정하지 않고 비워둡니다.
* **개인 작업 환경**: 각 작업자는 자신의 이름으로 된 전용 씬을 생성하여 테스트를 진행합니다. (예: `JaeinScene`)
* **씬 관리**: 모든 씬 파일(.unity)은 `00.Scenes` 폴더 내에 저장합니다.
* **버전 정보**: 프로젝트는 **Unity 6000.3.9f1** 버전을 사용합니다.

---

## **1. 폴더 구조 (Folder Structure)**
프로젝트 뷰의 가독성과 정렬을 위해 아래의 인덱싱 구조를 엄격히 따릅니다.

* **00.Scenes**: 씬 파일 (`.unity`)
* **01.Scripts**: C# 스크립트
* **02.Prefabs**: 재사용 가능한 프리팹
* **03.Art**: 3D 모델, 2D 소스, 메테리얼 등 아트 리소스
* **04.UI**: UI 프리팹, 아틀라스 및 스프라이트
* **05.Audio**: BGM, SFX 오디오 파일
* **06.VFX**: 파티클 시스템, 셰이더 및 이펙트
* **07.Data**: ScriptableObject, JSON, CSV 등 데이터 에셋
* **98.Debugger**: 디버그 전용 툴, 스크립트 및 테스트 프리팹
* **99.Test**: 개인별 샌드박스 씬 및 임시 테스트 스크립트

---

## **2. Git 브랜치 전략 (GitFlow)**
* **main**: 최종 배포 및 빌드용 브랜치.
* **dev**: 개발 통합 브랜치. 모든 기능 구현 결과가 모이는 중심.
* **feature/**: 단위 기능 구현 브랜치. (예: `feature/player-movement`)

---

## **3. 코드 컨벤션 (C# Naming Convention)**
* **PascalCase**: 클래스(Class), 메서드(Method), 프로퍼티(Property)
* **_camelCase**: `private` 및 `protected` 필드. 접두어 언더바(`_`) 사용 필수.
* **camelCase**: 지역 변수(Local Variable), 파라미터(Parameter)

---

## **4. 프로그래밍 규칙 (Programming Rules)**

### 가독성
* 지역 변수와 foreach에는 명시적 타입을 기본으로 사용합니다.
* 조건문과 반복문에는 중괄호를 사용하며, 조기 반환으로 중첩을 줄입니다.
* 한 줄에 여러 동작을 넣지 않습니다. 긴 메서드는 역할에 따라 분리합니다.
* 불필요한 추상화보다 명확한 이름과 실행 순서를 우선합니다.

### **필수 패키지 및 에셋 활용 (Required Packages)**
* **입력 시스템 (Input System)**: 레거시 Input Manager의 사용을 엄격히 금지하며, 오직 **New Input System**만을 사용합니다.
* **UI 텍스트 (UI Text)**: 레거시 Text 컴포넌트 사용을 금지하고, 반드시 **TextMeshPro (TMP)**를 사용합니다.
* **트위닝 (Tweening)**: 코드 기반의 애니메이션 및 트위닝 연출은 **DOTween**을 활용합니다.

### 앱·씬 수명과 초기화
* 앱은 `Resources/AppRoot.prefab` 하나로 시작하며 Bootstrapper가 루트 중복 방지와 DDOL을 담당합니다.
* 앱 서비스: GameManager, TimeManager, SoundManager, SceneLoader, InputReader.
* 씬 서비스: GameFlowManager, UIManager, CameraManager, PoolManager. SceneContext의 자식으로 배치하고 서비스 배열에 명시적으로 연결합니다.
* 순서: 필수 참조 검사 → Configure → 모든 Initialize → 모든 StartService → 최초 상태 변경.
* Initialize 실패 시 실행을 중단하고 역순 Shutdown합니다. MonoBehaviour.Awake에서 기능을 시작하지 않습니다.
* Singleton은 전역 접근 호환만 제공합니다. 매니저 간 의존성은 Configure로 전달합니다.
* 앱 서비스 구독은 OnStartService/OnStopService에 짝지어 작성합니다. 재활성화 시 자동 복원됩니다.
* 게임 상태는 RequestPause/RequestResume/BeginLoading/CompleteLoading/EndGame을 통해 변경합니다.
* GameFlowManager는 일시정지 중 진행 상태를 보존하고 전환 요청을 거절합니다.

### 통신과 객체 소유권
* 담당자가 하나인 요청은 메서드로 호출합니다. 여러 시스템에 전달하는 결과는 EventBus로 알립니다.
* 구독 시작 시 현재 상태를 읽어 화면과 처리를 동기화합니다.
* 풀 객체는 PooledObject에 기록된 소유 풀로 반환합니다. 이름으로 풀을 검색하지 않습니다.
* 풀은 씬 종료 시 대여 중인 객체와 대기 객체를 모두 정리합니다.
* Resources에서 로드하는 AppRoot는 하나만 유지합니다. 씬 안에 앱 매니저를 추가하지 않습니다.

### 설정 및 검증
1. Unity 메뉴 `Framework/Create Setup`은 없는 경우에만 AppRoot, 전용 입력 자산, `00.Scenes/CodexFrameworkScene.unity`를 생성합니다.
2. 게임 씬은 SceneContext에 초기 상태(Playing 또는 MainMenu)와 해당 씬 서비스를 연결합니다. UI 패널과 카메라 참조는 Inspector에서 지정합니다.
3. 씬 전환은 SceneLoader.RequestLoad로 요청합니다. 전환 대상 씬은 Build Profiles의 씬 목록에 등록해야 합니다.
4. 입력 기본값: WASD 이동, 마우스 위치, 좌/우 클릭, Enter 확인, Backspace 취소, Escape 일시정지.
5. 배치 검증 진입점은 `FrameworkSetup.Validate`입니다. 상태 전환·일시정지 보존·잘못된 전환 거절을 검사합니다.
6. 검증 씬은 실행 상태·경과 시간·움직이는 표시와 일시정지/재개 버튼을 제공합니다. Escape로도 조작할 수 있습니다.
7. Editor의 Domain Reload와 Scene Reload는 활성화한 구성을 기준으로 합니다.

### **엄격한 예외 처리 (Strict Null Check)**
* **에러 로그 강제**: 참조 확인 시 단순 `return` 처리를 금지합니다. 반드시 `Debug.LogError()`를 호출하여 콘솔에 에러를 명시하고 로직을 즉시 중단합니다.

---

## **5. 작업 흐름 요약**
1. `dev`에서 `feature/기능-이름` 브랜치 생성.
2. 자신의 이름으로 된 테스트 씬에서 기능을 구현.
3. 머지 전 `dev`를 자신의 브랜치로 가져와(Pull/Merge) 충돌 해결.
4. 컴파일 에러 및 로그 상 결함이 없는 상태로 `dev`에 병합.
