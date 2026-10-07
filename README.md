# 논산 선샤인랜드 차량 전투 시뮬레이터

HMD 없이 Vive Tracker 총기로 스크린을 조준하는 4인 탑승형 레일 슈팅 시뮬레이터.
주행 경로에 맞춰 모션 시뮬레이터가 기울어지며, 차량전 3스테이지에 보스(헬기, 탱크)와 파괴 목표(레이더, 미사일 발사대 등)가 등장한다.

| 항목 | 내용 |
|---|---|
| 기간 | 2026.04 ~ 2026.06 개발 / 2026.07 유지보수 |
| 인원 | 2인 개발 |
| 담당 | 트래커 장치 연동을 제외한 클라이언트 전반 (조준·사격, 적·보스 전투, 시뮬레이터 연동, 사운드·UI) |
| 플랫폼 | Windows PC + 와이드 스크린, Vive Tracker + Base Station, 모션 시뮬레이터 |
| 기술 | Unity 6, C#, SteamVR(OpenVR), Cinemachine Spline, URP, UDP |

> **안내**
> - 납품 프로젝트에서 본인 담당 코드 중 핵심 시스템만 발췌한 저장소로, 단독으로 빌드되지 않는다.
> - 공개를 위해 주석 정리 및 일부 리팩터링을 거친 버전으로, 납품 빌드와 세부 구현이 다를 수 있다.
> - 트래커 장치 연동(디바이스 시리얼 매칭, 포즈 수신)은 협업자 담당이라 포함하지 않았다.

---

## 폴더 구성

### `Scripts/Aim` — HMD 없는 트래커 조준 파이프라인
트래커 회전 → 가상 보드 UV → 에임 UI 좌표 → 카메라 레이 순서로 변환해, 화면에 보이는 에임 위치와 실제 피격 위치를 일치시킨다.

| 파일 | 역할 |
|---|---|
| `Tracker.cs` | 트래커 상대 회전을 축 변환·감도·클램핑 후 조준용 오브젝트에 적용 |
| `AimBoardFitter.cs` | 카메라 FOV와 Canvas 비율로 가상 보드 크기를 자동 계산 (해상도 무관) |
| `TrackerRay.cs` | 보드에 맞은 지점을 UV로 정규화해 에임 UI 위치로 매핑 |
| `ShotRay.cs` | 에임 UI 위치에서 카메라 레이로 피격 판정, 표면별 이펙트 |

### `Scripts/Simulator` — 주행 경로 기반 모션 시뮬레이터 연동
| 파일 | 역할 |
|---|---|
| `SimulatorUDP.cs` | 스플라인 접선 변화량을 횡방향에 투영해 커브 방향·세기를 Roll로 변환, UDP 송신. 충돌 8방향 신호, 급정지 Pitch 연출 |
| `SplineMovement.cs` | 같은 곡률 계산으로 커브 구간 자동 감속, 일시 가속·감속 |
| `Packet.cs` | 시뮬레이터 송수신 패킷 구조체 |

### `Scripts/Boss` — 확장 가능한 보스 부위 파괴 시스템
보스(헬기, 탱크)와 파괴 목표(레이더, 미사일 발사대 등)가 `MarkerBase` 하나를 공유한다. 새 대상은 파괴 연출(`OnVehicleDestroyed`)만 구현하면 된다.

| 파일 | 역할 |
|---|---|
| `MarkerBase.cs` | 약점을 정해진 순서로 활성화, 전부 파괴되면 보스 파괴 처리 |
| `VehicleWeakness.cs` | 부위 파괴 이벤트 발생, 피격 보너스와 처치 점수 분리 |
| `MarkerManager.cs` / `TargetMarker.cs` | 약점 마커 UI 풀링, 콜라이더 8개 꼭짓점을 화면에 투영해 마커 크기 자동 조절 |
| `HeliAttackController.cs` | 무기와 약점 부위 매핑. 부위 파괴 시 대응 무기 무력화, 파괴 수에 따라 공격 강도 감소 |
| `BossAttackController.cs` | 보스 공격 컨트롤러 베이스 (인스펙터 직렬화 참조를 위해 인터페이스 대신 추상 클래스) |
| `PathTriggerDetector.cs` | 경로 통과 시 보스 전투 시작·종료 |
| `BaseEnemy.cs` / `CombatEnemy.cs` / `BaseHitBox.cs` | 일반 적과 보스 약점이 피격·사망·점수 흐름을 공유하는 상속 구조, 제네릭 히트박스 |

### `Scripts/Enemy` — 위협감 중심의 적 연출
| 파일 | 역할 |
|---|---|
| `HelicopterMissileBase.cs` | 반복 수렴 예측 조준 + 카메라 기준 의도적 빗나감 오프셋 |
| `MissileProjectileBase.cs` | 약한 호밍, 근접·수명 폭발, 근거리 폭발 시 플레이어 피격 연출 |
| `HelicopterLv3.cs` | 진입 → 비대칭 Perlin 호버(현장 스크린 배치에 맞춘 활동 영역) → 이탈 페이즈 |
| `OtherHelis.cs` | 카메라 로컬 좌표 기준 Catmull-Rom 경로로 화면을 스쳐가는 서브 헬기 |
| 기타 | 무기(미니건, 소형·대형 미사일 발사대), 미사일 비행체, 추락 연출 유틸 |

### `Scripts/Common`
| 파일 | 역할 |
|---|---|
| `SoundManager.cs` | 이름 기반 사운드 조회, SFX AudioSource 풀링, BGM 페이드 전환 |
| `TitanWeapon.cs` | 포격 시 머즐 플래시, Chromatic Aberration 페이드 (런타임 프로필 사본 사용) |

---

## 포함하지 않은 의존 코드
`PlayerData`, `EnemyManager`, `ScoreManager`, `TrailPool`, `PooledBulletTrail`, `IDamageable`, `IEnemy`, `HitSurfaceType`, `SoundClipCollection`, `TrackerInput`, `Lv3HeliTrigger`, SteamVR 플러그인 등
