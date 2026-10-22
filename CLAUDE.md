# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# Unity 메모리/GC 벤치마크 프로젝트

## 목적
- 스터디(유니티 성능 최적화 책 7~10장) 실습용.
- Instantiate/Destroy 반복, ObjectPool, 의도적 메모리 누수 3가지를
  Unity Profiler와 Memory Profiler로 비교 측정하고 발표 자료를 만드는 것.

## 환경
- Unity: 6000.3.27f1 (Unity 6)
- OS: Windows
- 측정은 항상 Development Build 기준. 에디터 수치는 측정에 사용하지 않음.

## 작업 규칙
- 스크립트는 Assets/Scripts 폴더에만 추가한다.
- .meta 파일, Library/, ProjectSettings/는 요청 없이 수정하지 않는다.
- 씬과 프리팹(YAML) 파일은 직접 편집하지 않는다. 필요한 오브젝트는 코드로 생성한다.
- 코드를 쓰기 전에 파일 구조와 클래스 설계를 먼저 보여주고 승인을 받는다.
- 텍스트 UI는 GUI.Label(OnGUI)만 사용한다. TextMeshPro는 쓰지 않는다.

## GC Alloc 오염 방지 (측정 대상 코드의 규칙)
- Update/스폰 경로에서 LINQ, FindObjectOfType, 반복 GetComponent, 문자열 연결 금지.
- 코루틴과 Destroy(go, t)로 수명을 관리하지 않는다. Update에서 deltaTime을 누적한다.
- 람다/delegate를 매번 새로 만들지 않는다.
- 난수는 고정 시드(Random.InitState)를 쓴다.

## 측정 규칙 (참고)
- 수치 비교는 모드마다 앱을 새로 실행해서 측정. 런타임 모드 전환은 데모용.
- 같은 시드, 같은 스폰량, 같은 시간 조건으로 비교한다.
- 이번 단계에서는 Incremental GC 등 Player Settings 비교, CSV 로거, 추가 씬은 만들지 않는다.
