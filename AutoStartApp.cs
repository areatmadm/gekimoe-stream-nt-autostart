using OBSWebsocketDotNet; // 최신 v5 전용
using Photino.NET;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Reflection;

namespace GEKImoeStreamNTAutoStart
{
    public class AutoStartApp
    {
        private string vender = "NOLJA";
        private string vender_swdf = "";
        private string p = "";
        private int pc_num = 0;

        private readonly HttpClient httpClient = new HttpClient();

        public void Run()
        {
            // 실행 파일 위치 기반으로 아이콘 경로 설정 (Windows 10 오류 방지)
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string iconPath = Path.Combine(baseDir, "wwwroot", "assets", "main.ico");

            var window = new PhotinoWindow()
                .SetTitle("GEKImoe Stream NT AutoStart")
                .SetIconFile(iconPath)
                .SetUseOsDefaultLocation(false)
                .SetUseOsDefaultSize(false)
                .SetSize(853, 449)
                .Center()
                .SetResizable(false)
                .SetChromeless(true)
                .SetTopMost(true)
                .RegisterWindowCreatedHandler((sender, e) =>
                {
                    var win = (PhotinoWindow)sender!;
                    // Windows 11 이상에서만 둥근 모서리 적용 (WindowHelper 내부에서 OS 체크 수행)
                    WindowHelper.ApplyRoundedCorners(win);

                    Task.Run(async () => await RunAutoStartSequence(win));
                })
                .Load("wwwroot/autostart.html");

            window.WaitForClose();
        }

        private void UpdateUI(PhotinoWindow window, string targetId, string text)
        {
            var data = JsonSerializer.Serialize(new { target = targetId, value = text });
            window.SendWebMessage(data);
        }

        private async Task RunAutoStartSequence(PhotinoWindow window)
        {
            try
            {
                // 0. Windows 11은 기본적으로 22000 이상이므로 함께 통과됩니다.
                int buildVersion = Environment.OSVersion.Version.Build;

                if (buildVersion < 19044)
                {
                    UpdateUI(window, "name", "시스템 호환성 오류");
                    UpdateUI(window, "pg", "지원되지 않는 Windows 버전입니다.");
                    UpdateUI(window, "info", "GEKImoe Stream Assistant는 Windows 10(21H2 이상)과 Windows 11을 지원합니다. 먼저 Windows 업데이트를 실행하여 21H2 이상으로 빌드를 업데이트한 후 이 어시스턴트를 다시 실행해 주세요.");

                    await Task.Delay(5000);
                    Process.Start("explorer.exe"); // 종료 전 익스플로러 복구
                    await Task.Delay(2000);
                    Environment.Exit(0);
                    return;
                }

                // 0. Explorer 강제 종료 (키오스크 모드 진입)
                Process.Start(new ProcessStartInfo { FileName = "taskkill.exe", Arguments = "/f /im explorer.exe", WindowStyle = ProcessWindowStyle.Hidden });
                await Task.Delay(1000);

                UpdateUI(window, "name", "방송PC를 준비하고 있습니다...");
                UpdateUI(window, "info", "Do Not Disturb\r\n시스템을 건드리지 마세요.");

                // 1-1. 프로그램 버전 가져오기 (예: 6.0.0)
                string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "6.0.0";

                // 1-2. .csproj에 수동으로 적은 BuildDateTime 꺼내오기
                var buildDateAttribute = Assembly.GetExecutingAssembly()
                    .GetCustomAttributes<AssemblyMetadataAttribute>()
                    .FirstOrDefault(a => a.Key == "BuildDateTime");

                string buildTime = buildDateAttribute?.Value ?? "000000000000";

                // 1-3. 값 조합 (결과: 6.0.0_202602212036)
                string finalVersion = $"{version}_{buildTime}";

                // 1-4. HTML의 <span> 태그(lbl_nowver)로 값 쏘기!
                UpdateUI(window, "nowver", finalVersion);

                UpdateUI(window, "pg", "초기화 및 파일 정리 중...");

                if (File.Exists("game_set.txt")) p = File.ReadAllText("game_set.txt").Trim();
                if (File.Exists("vender.txt")) vender = File.ReadAllText("vender.txt").Trim();
                if (File.Exists("game_pc_num.txt")) int.TryParse(File.ReadAllText("game_pc_num.txt"), out pc_num);

                // 2. 불필요한 파일 삭제
                string[] filesToDelete = { "restart_pc.bat", "restart_pc.vbs", "upd_version", "AreaTM_acbas_updater_1.exe", "SangguGSA5.exe", "autoobs.lnk" };
                foreach (var file in filesToDelete) { if (File.Exists(file)) File.Delete(file); }
                if (Directory.Exists("cache_drumchat")) Directory.Delete("cache_drumchat", true);
                if (Directory.Exists("locales")) Directory.Delete("locales", true);

                await Task.Delay(1000);

                // 3. 서버 인증 및 플랜 확인
                AuthManager authManager = new AuthManager(httpClient);
                AuthResult authResult = await authManager.AuthenticateAsync(vender, p, msg => UpdateUI(window, "pg", msg));

                if (!authResult.IsSuccess)
                {
                    UpdateUI(window, "pg", authResult.ErrorMessage ?? "인증 실패");
                    return;
                }
                vender_swdf = authResult.Plan ?? "";

                // ==============================================================================
                // 4: 업데이트 확인 로직 (기존 WinForms 로직 이식)
                // ==============================================================================
                if (!File.Exists("error_update"))
                {
                    UpdateUI(window, "pg", "현재 버전 정보 확인 중... (1/4)");

                    if ((vender_swdf == "full" || vender_swdf == "mini") && File.Exists("AreaTM_acbas.exe"))
                    {
                        // 기존 잔류 파일 제거
                        if (File.Exists("_CheckVersion_")) File.Delete("_CheckVersion_");
                        if (File.Exists("_ChecPartNUM_")) File.Delete("_ChecPartNUM_");

                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "AreaTM_acbas.exe",
                            Arguments = "getver",
                            WindowStyle = ProcessWindowStyle.Hidden,
                            UseShellExecute = false // 프로세스 실행의 확실성을 위해 추가
                        });

                        // [개선] 파일이 생성될 때까지 최대 10초간 대기 (0.5초 간격)
                        bool isReady = false;
                        for (int i = 0; i < 20; i++)
                        {
                            if (File.Exists("_CheckVersion_") && File.Exists("_ChecPartNUM_"))
                            {
                                isReady = true;
                                break;
                            }
                            UpdateUI(window, "pg", $"버전 파일 생성 대기 중... ({i + 1}/20) (2/4)");
                            await Task.Delay(500);
                        }

                        if (isReady)
                        {
                            UpdateUI(window, "pg", "서버와 버전을 대조하고 있습니다... (3/4)");
                            await Task.Delay(200);
                            string acbas_build = File.ReadAllText("_CheckVersion_").Trim();
                            string acbas_partnum = File.ReadAllText("_ChecPartNUM_").Trim().Replace("\r\n", "");

                            UpdateUI(window, "pg", "업데이트 확인 중... (4/4)");
                            await Task.Delay(200);

                            var postData = new Dictionary<string, string>
                            {
                                { "mod", "1" },
                                { "ver", acbas_build },
                                { "winver", Environment.OSVersion.Version.Major.ToString() },
                                { "winbuild", Environment.OSVersion.Version.Build.ToString() }
                            };

                            var content = new FormUrlEncodedContent(postData);
                            var response = await httpClient.PostAsync($"https://update-check.stream-assistant-5.gekimoe.areatm.com/{acbas_partnum}/", content);
                            string result = await response.Content.ReadAsStringAsync();

                            // 파일 정리
                            File.Delete("_CheckVersion_");
                            File.Delete("_ChecPartNUM_");

                            if (result.Trim() == "1")
                            {
                                UpdateUI(window, "pg", "새로운 업데이트가 발견되었습니다. 업데이트를 진행합니다.");
                                await Task.Delay(3000);

                                UpdaterApp updater = new UpdaterApp();
                                await updater.Run(window, vender, acbas_partnum, acbas_build, p);
                                return; // 업데이트로 진입했으므로 이후 로직 중단
                            }

                            // 업데이트가 없는 경우에만 아래로 내려감
                            UpdateUI(window, "pg", "최신 버전입니다. 시스템 구동을 계속합니다.");
                        }
                        else
                        {
                            // 💡 10초가 지나도 파일이 안 만들어진 경우
                            UpdateUI(window, "pg", "[오류] 버전 정보를 생성하지 못했습니다. (AreaTM_acbas.exe 작동 불능)");
                            await Task.Delay(3000);
                        }
                    }
                    else
                    {
                        // 💡 조건(Plan 혹은 Exe 없음) 자체가 안 맞아서 스킵된 경우
                        UpdateUI(window, "pg", "업데이트 확인 조건을 만족하지 않아 건너뜁니다.");
                        await Task.Delay(2000);
                    }
                }

                // 5. OBS 구동
                var obsProcs = Process.GetProcessesByName("obs64");
                if (obsProcs.Length == 0)
                {
                    // 5-1. Safe Mode 여부 확인
                    UpdateUI(window, "pg", "OBS Studio를 시작하기 전 안전모드 여부 확인중...");
                    await Task.Delay(900); // 일시 대기

                    // 2026.2.19 : OBS 32 Logic changed - Enforce Safety mode disable
                    string obsPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\obs-studio\";

                    // safe_mode(파일) 혹은 .sentinel(폴더)이 존재하는지 확인
                    if (File.Exists(obsPath + "safe_mode") || Directory.Exists(obsPath + ".sentinel"))
                    {
                        UpdateUI(window, "pg", "OBS Studio를 시작하기 전 안전모드 비활성화 중...");
                        await Task.Delay(1000);

                        try { File.Delete(obsPath + "safe_mode"); } catch { }

                        // .sentinel이 폴더이므로 내부 파일까지 강제 삭제 (true = recursive)
                        try { if (Directory.Exists(obsPath + ".sentinel")) Directory.Delete(obsPath + ".sentinel", true); } catch { }
                    }

                    obsPath = @"C:\Program Files\obs-studio\bin\64bit\obs64.exe";
                    UpdateUI(window, "pg", "OBS Studio를 시작합니다...");
                    if (File.Exists(obsPath))
                    {
                        Process.Start(new ProcessStartInfo { FileName = obsPath, WorkingDirectory = @"C:\Program Files\obs-studio\bin\64bit\" });
                        await Task.Delay(8000); // OBS 부팅 대기
                    }
                }

                // ==============================================================================
                // 6. OBS 웹소켓 동기화 (v5 전용)
                // ==============================================================================
                UpdateUI(window, "pg", "OBS Studio 연동 시도 중...");
                bool isObsConnected = false;

                try
                {
                    OBSWebsocketDotNet.OBSWebsocket obsV5 = new OBSWebsocketDotNet.OBSWebsocket();
                    obsV5.ConnectAsync("ws://127.0.0.1:4455", "noljabroadcastpc");

                    // 연결 완료까지 최대 5초간 대기 (0.2초 간격 체크)
                    for (int i = 0; i < 25; i++)
                    {
                        if (obsV5.IsConnected) break;
                        await Task.Delay(200);
                    }

                    if (obsV5.IsConnected)
                    {
                        UpdateUI(window, "pg", "OBS Studio 연결 성공!");
                        isObsConnected = true;

                        var streamStatus = obsV5.GetStreamStatus();
                        if (!streamStatus.IsActive)
                        {
                            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                            string webCamDir = Path.Combine(baseDir, "WebCameraConfig");
                            string batPath = Path.Combine(webCamDir, "restore.bat");
                            string cfgPath = Path.Combine(webCamDir, "cam_sett.cfg");

                            // 웹캠 복구 스크립트 실행
                            if (File.Exists(cfgPath))
                            {
                                UpdateUI(window, "pg", "웹캠 설정을 로드합니다...");

                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = batPath, // "restore.bat" 대신 완벽한 절대 경로 사용
                                    WorkingDirectory = webCamDir,
                                    WindowStyle = ProcessWindowStyle.Hidden,
                                    UseShellExecute = true // [핵심] .bat 파일을 실행하려면 무조건 true여야 합니다!
                                });

                                await Task.Delay(2000);
                            }

                            UpdateUI(window, "pg", "스트리밍을 시작합니다...");
                            obsV5.StartStream();
                            await Task.Delay(2890);

                            // 씬 전환 (캠 초기화)
                            obsV5.SetCurrentProgramScene("camoff");
                            await Task.Delay(1000);
                            obsV5.SetCurrentProgramScene("camon");
                        }
                        else
                        {
                            UpdateUI(window, "pg", "이미 스트리밍이 진행 중입니다.");
                        }
                        obsV5.Disconnect();
                    }
                }
                catch (Exception ex)
                {
                    UpdateUI(window, "pg", $"OBS 연결 오류: {ex.Message}");
                    await Task.Delay(2000);
                }

                if (!isObsConnected)
                {
                    UpdateUI(window, "pg", "OBS 웹소켓 연결에 실패했습니다. 설정을 확인하세요.");
                    await Task.Delay(3000);
                }

                // 7. 시스템 복구 및 메인 앱 실행
                UpdateUI(window, "pg", "설정이 완료되었습니다. 곧 GEKImoe Stream이 실행됩니다.");
                Process.Start("explorer.exe");
                await Task.Delay(4000);

                if ((vender_swdf == "full" || vender_swdf == "mini") && File.Exists("AreaTM_acbas.exe"))
                {
                    Process.Start(Path.GetFullPath("AreaTM_acbas.exe"));
                }

                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                UpdateUI(window, "pg", $"오류 발생: {ex.Message}");
            }
        }
    }
}