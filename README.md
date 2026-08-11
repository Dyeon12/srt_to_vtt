# SRT → VTT 자막 변환기

> 한국어 SRT 자막을 다양한 플랫폼과 웹 플레이어에서 사용할 수 있는 WebVTT로 변환하는 Windows 데스크톱 프로그램

![Windows](https://img.shields.io/badge/Windows-64bit-0078D4?logo=windows&logoColor=white)
![.NET Framework](https://img.shields.io/badge/.NET_Framework-4.x-512BD4)
![Output](https://img.shields.io/badge/Output-WebVTT-635BFF)

SRT → VTT 자막 변환기는 별도의 설치 과정 없이 실행할 수 있는 한국어 자막 일괄 변환 도구입니다.

한글 인코딩을 자동으로 판별하고, 국내 LMS 및 Windows 프로그램에서 글자가 깨지지 않도록 UTF-8 BOM 형식으로 결과를 저장합니다.

## 주요 기능

| 기능 | 설명 |
|---|---|
| 다중 파일 변환 | 여러 개의 SRT 자막을 한 번에 VTT로 변환합니다. |
| 드래그 앤 드롭 | 탐색기에서 SRT 파일을 창으로 바로 끌어놓을 수 있습니다. |
| 한글 인코딩 보호 | UTF-8, UTF-16, CP949 입력을 자동으로 감지합니다. |
| 호환성 높은 출력 | 결과 파일을 UTF-8 BOM WebVTT로 저장합니다. |
| 시간 보정 | 초 단위 양수·음수 값을 사용해 전체 자막 시간을 이동합니다. |
| 출력 위치 선택 | 원본 폴더 또는 원본 폴더의 `VTT` 하위 폴더에 저장합니다. |
| 안전한 덮어쓰기 | 기존 VTT 파일을 덮어쓸지 직접 선택할 수 있습니다. |
| 진행 상태 표시 | 변환 진행률과 파일별 오류를 한국어로 안내합니다. |

## 빠른 시작

1. [`배포/SRT-to-VTT-Converter-Windows-x64.exe`](배포/SRT-to-VTT-Converter-Windows-x64.exe)를 내려받아 실행합니다.
2. `SRT 추가` 버튼을 누르거나 SRT 파일을 프로그램 창에 끌어놓습니다.
3. 저장 위치, 덮어쓰기 여부, 시간 보정값을 설정합니다.
4. `VTT 변환 시작`을 누릅니다.
5. 변환이 끝나면 `결과 열기` 버튼으로 출력 폴더를 확인합니다.

설치 프로그램이나 별도의 외부 라이브러리는 필요하지 않습니다.

## 시간 보정 사용법

시간 보정값은 초 단위로 입력합니다.

| 입력값 | 결과 |
|---:|---|
| `0.000` | 원래 시간을 유지합니다. |
| `1.500` | 모든 자막을 1.5초 늦춥니다. |
| `-2.000` | 모든 자막을 2초 앞당깁니다. |

보정 결과가 0초보다 앞서는 자막은 자동으로 `00:00:00.000`부터 시작합니다.

## 인코딩 정책

입력 파일은 다음 순서로 판별합니다.

1. UTF-8 BOM
2. UTF-16 Little Endian / Big Endian
3. BOM 없는 UTF-8
4. 한국어 ANSI(CP949)

출력 VTT는 항상 **UTF-8 BOM**으로 저장합니다. BOM 없는 UTF-8을 ANSI로 잘못 인식하는 일부 Windows 편집기와 국내 자막 플랫폼에서도 한글이 정상적으로 표시되도록 하기 위한 설정입니다.

## 직접 빌드하기

### 요구 사항

- Windows 10 또는 Windows 11 64비트
- .NET Framework 4.x
- .NET Framework MSBuild

### 빌드 방법

저장소를 내려받은 후 루트 폴더의 `빌드.bat`을 실행합니다.

```bat
빌드.bat
```

Release 빌드가 완료되면 아래 위치에 단일 실행 파일이 생성됩니다.

```text
배포\SRT-to-VTT-Converter-Windows-x64.exe
```

## 프로젝트 구조

```text
srt_to_vtt/
├─ KoreanSubtitleStudio/
│  ├─ Services/
│  │  └─ SubtitleConversionService.cs  # 인코딩 판별 및 변환 엔진
│  ├─ MainWindow.xaml                   # 데스크톱 UI
│  ├─ MainWindow.xaml.cs                # 파일 선택 및 작업 흐름
│  └─ KoreanSubtitleStudio.csproj
├─ 배포/
│  └─ SRT-to-VTT-Converter-Windows-x64.exe
├─ KoreanSubtitleStudio.sln
└─ 빌드.bat
```

## 변환 형식 예시

SRT 입력:

```srt
1
00:00:01,250 --> 00:00:03,500
안녕하세요. 자막 변환을 시작하겠습니다.
```

WebVTT 출력:

```vtt
WEBVTT

00:00:01.250 --> 00:00:03.500
안녕하세요. 자막 변환을 시작하겠습니다.
```

## 참고 사항

- 출력 파일의 확장자는 `.vtt`입니다.
- 동일한 이름의 VTT가 존재하고 덮어쓰기를 선택하지 않으면 해당 파일은 변환하지 않습니다.
- 손상되었거나 유효한 타임라인이 없는 SRT는 오류 메시지로 안내합니다.
- Windows용 WPF 애플리케이션이므로 macOS와 Linux에서는 직접 실행할 수 없습니다.
