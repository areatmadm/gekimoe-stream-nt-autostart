using Photino.NET;
using SevenZip;
using System.Diagnostics;
using System.Text.Json;

namespace GEKImoeStreamUpdaterNT
{
    public class UpdateApp
    {
        public void Run()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string iconPath = Path.Combine(baseDir, "wwwroot", "assets", "updater.ico");

            var window = new PhotinoWindow()
                .SetTitle("GEKImoe Stream NT Updater")
                .SetIconFile(iconPath)
                .SetUseOsDefaultLocation(false)
                .SetUseOsDefaultSize(false)
                .SetSize(750, 220)
                .Center()
                .SetResizable(false)
                .SetChromeless(true)
                .SetTopMost(true)
                .RegisterWindowCreatedHandler((sender, e) =>
                {
                    var win = (PhotinoWindow)sender!;

                    // Windows 10/11 호환성을 위한 둥근 모서리 적용 로직 (생략 또는 외부 클래스 참조)
                    // WindowHelper.ApplyRoundedCorners(win);

                    Task.Run(async () => await RunExtractionSequence(win));
                })
                .Load("wwwroot/updater.html");

            window.WaitForClose();
        }

        private void UpdateUI(PhotinoWindow window, string statusText)
        {
            var data = JsonSerializer.Serialize(new { target = "status", value = statusText });
            window.SendWebMessage(data);
        }

        private async Task RunExtractionSequence(PhotinoWindow window)
        {
            try
            {
                await Task.Delay(2000);

                UpdateUI(window, "압축 해제 준비 중...");
                await Task.Delay(5000); // 원본 코드의 대기 시간 유지

                UpdateUI(window, "압축 해제 중...");
                await Task.Delay(1000);

                // SevenZip 초기화 (7z.dll 경로 지정)
                string sevenZipDllPath = Path.GetFullPath("7z.dll");
                if (!File.Exists(sevenZipDllPath))
                {
                    throw new FileNotFoundException("7z.dll 파일을 찾을 수 없습니다.");
                }
                SevenZipBase.SetLibraryPath(sevenZipDllPath);

                string targetArchive = Path.GetFullPath("gekimoe_acbas_update.7z");
                if (!File.Exists(targetArchive))
                {
                    throw new FileNotFoundException("업데이트 파일(gekimoe_acbas_update.7z)을 찾을 수 없습니다.");
                }

                // 압축 해제 (이벤트 기반 비동기 처리)
                string currentDir = Directory.GetCurrentDirectory();

                await Task.Run(() =>
                {
                    using (var extractor = new SevenZipExtractor(targetArchive, "GekiMoeAssistant@withAreaTM00115!"))
                    {
                        // 구형 Begin 메서드 대신, 스레드를 안전하게 블로킹하는 동기식 메서드를 사용합니다.
                        // Task.Run 안에서 돌기 때문에 UI가 멈추지 않습니다!
                        extractor.ExtractArchive(currentDir);
                    }
                });

                // --- 압축 해제 완료 후 처리 ---
                await Task.Delay(200);

                // 임시 파일 삭제
                try { File.Delete("gekimoe_acbas_update.7z"); } catch { }

                // 성공 마커 생성
                File.WriteAllText("update_success", "");

                UpdateUI(window, "압축 해제 완료! 재실행 중...");
                await Task.Delay(2000);

                // 새 버전의 AutoStart 실행
                if (File.Exists("AutoStartNT.exe"))
                {
                    Process.Start("AutoStartNT.exe");
                }
                else if (File.Exists("AutoStartV3.exe")) // 구버전 폴백
                {
                    Process.Start("AutoStartV3.exe");
                }

                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                UpdateUI(window, $"압축 해제 중 오류 발생: {ex.Message}");
                File.WriteAllText("error_update", "");

                try { File.Delete("gekimoe_acbas_update.7z"); } catch { }

                await Task.Delay(3000);

                // 오류 시에도 메인 프로그램은 다시 살려줍니다.
                if (File.Exists("AutoStartNT.exe")) Process.Start("AutoStartNT.exe");
                else if (File.Exists("AutoStartV3.exe")) Process.Start("AutoStartV3.exe");

                Environment.Exit(1);
            }
        }
    }
}