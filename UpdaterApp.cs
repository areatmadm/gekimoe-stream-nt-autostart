using Photino.NET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace GEKImoeStreamNTAutoStart
{
    public class UpdaterApp
    {
        private readonly HttpClient _httpClient = new HttpClient();

        public async Task Run(PhotinoWindow window, string vender, string partNum, string build, string game)
        {
            // 💡 [핵심] 새 창을 띄우지 않고, AutoStartApp에서 쓰던 창을 그대로 재활용합니다!
            // 크기, 제목, HTML만 업데이터용으로 매끄럽게 변경합니다.

            window.SetTitle("GEKImoe Stream NT Updater");
            window.SetSize(750, 220);
            window.Center();

            // UI를 업데이터 화면으로 전환
            window.Load("wwwroot/updater.html");

            // HTML이 로드될 수 있도록 아주 짧은 시간을 줍니다.
            await Task.Delay(500);

            // 창 충돌 없이 안전하게 바로 업데이트 시퀀스 시작
            await RunUpdateSequence(window, vender, partNum, build, game);
        }

        private void UpdateUI(PhotinoWindow window, string statusText)
        {
            var data = JsonSerializer.Serialize(new { target = "status", value = statusText });
            window.SendWebMessage(data);
        }

        private async Task RunUpdateSequence(PhotinoWindow window, string vender, string partNum, string build, string game)
        {
            try
            {
                // STEP 1: 준비 대기
                UpdateUI(window, "업데이트 준비 중...");
                await Task.Delay(5000);

                // STEP 2: 업데이트 버전 확인 (POST)
                UpdateUI(window, "업데이트 정보를 받는 중...");
                var checkData = new Dictionary<string, string>
                {
                    { "ver", build },
                    { "winver", Environment.OSVersion.Version.Major.ToString() },
                    { "winbuild", Environment.OSVersion.Version.Build.ToString() }
                };
                var checkContent = new FormUrlEncodedContent(checkData);
                var checkResp = await _httpClient.PostAsync($"https://update-check.stream-assistant-5.gekimoe.areatm.com/{partNum}/", checkContent);
                string downver = (await checkResp.Content.ReadAsStringAsync()).Trim();

                await Task.Delay(3000);

                // STEP 3: 파일 다운로드
                string downloadUrl = $"https://download.stream-assistant-5.gekimoe.areatm.com/sa/{partNum}/{downver}.7z";
                string targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gekimoe_acbas_update.7z");

                UpdateUI(window, "다운로드 중...");
                var fileBytes = await _httpClient.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(targetPath, fileBytes);

                UpdateUI(window, "다운로드 완료...");
                await Task.Delay(2000);

                // STEP 4: 프로세스 종료
                string[] targets = { "Nolja_OpenUp", "AreaTM_IoT" };
                foreach (var target in targets)
                {
                    foreach (var proc in Process.GetProcessesByName(target))
                    {
                        try { proc.Kill(); } catch { }
                    }
                }
                await Task.Delay(200);

                // STEP 5: 상태 보고
                UpdateUI(window, "서버에 상태를 보고합니다...");
                var statusData = new Dictionary<string, string>
                {
                    { "vender", vender },
                    { "game", game },
                    { "mode", "3" },
                    { "submode", "1" }
                };
                await _httpClient.PostAsync("https://service.stream-assistant-5.gekimoe.areatm.com/v2/serverstatus/v1/", new FormUrlEncodedContent(statusData));

                // STEP 6: 업데이터 실행
                UpdateUI(window, "업데이터를 실행합니다...");
                if (File.Exists("GEKImoeStreamUpdaterNT.exe"))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "GEKImoeStreamUpdaterNT.exe",
                        UseShellExecute = true
                    });
                }
                else if (File.Exists("AreaTM_acbas_updater_0.exe"))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "AreaTM_acbas_updater_0.exe",
                        UseShellExecute = true
                    });
                }

                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                UpdateUI(window, $"오류 발생: {ex.Message}");
                File.WriteAllText("error_update", "");
                await Task.Delay(3000);
                Environment.Exit(1);
            }
        }
    }
}