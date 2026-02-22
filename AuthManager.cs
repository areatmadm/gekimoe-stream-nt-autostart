using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace GEKImoeStreamNTAutoStart
{
    // 인증 결과를 담을 전용 그릇
    public class AuthResult
    {
        public bool IsSuccess { get; set; }
        public string? Plan { get; set; }         // 💡 여기 ? 추가
        public string? ErrorMessage { get; set; } // 💡 여기 ? 추가
    }

    public class AuthManager
    {
        private readonly HttpClient _httpClient;

        public AuthManager(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // POST 통신 공통 헬퍼
        private async Task<string> PostDataAsync(string url, string postData)
        {
            try
            {
                var content = new StringContent(postData, Encoding.UTF8, "application/x-www-form-urlencoded");
                var response = await _httpClient.PostAsync(url, content);
                return await response.Content.ReadAsStringAsync();
            }
            catch { return "__Error__"; }
        }

        // ==============================================================================
        // 🚀 통합 인증 진입점 (여기서 NT와 GSA5를 분기합니다)
        // ==============================================================================
        public async Task<AuthResult> AuthenticateAsync(string vender, string gameCode, Action<string> updateUI)
        {
            // 💡 [개발 예정 구역] 1. GEKImoe Stream NT 신규 인증 로직
            // 추후 NT 인증 API가 개발되면 아래 bool 값을 true로 바꾸고 로직을 구현하세요.
            bool isNTAuthReady = false;
            if (isNTAuthReady)
            {
                updateUI("GEKImoe Stream NT 서버 인증을 시도합니다...");
                // return await AuthenticateNTAsync(vender, gameCode, updateUI);
            }

            // 💡 [시한부 연동 구역] 2. 기존 GEKImoe Stream Assistant 5 폴백 (2027년 1월 종료 예정)
            updateUI("GEKImoe Stream Assistant 5 인증을 시작합니다...");
            await Task.Delay(1000);
            return await AuthenticateLegacyGSA5Async(vender, gameCode, updateUI);
        }

        // ==============================================================================
        // ⏳ 기존 GSA 5 전용 인증 로직 (2027.01 Deprecated)
        // ==============================================================================
        private async Task<AuthResult> AuthenticateLegacyGSA5Async(string vender, string gameCode, Action<string> updateUI)
        {
            string get_auth = "";

            // 1단계: 라이선스 인증
            while (true)
            {
                var now = DateTime.Now;
                if ((now.Hour == 5 && now.Minute >= 10) || now.Hour == 6)
                {
                    updateUI("GEKImoe Stream Assisatnt 5 서버 정기점검 중입니다...");
                    await Task.Delay(5000);
                    continue;
                }

                get_auth = await PostDataAsync("https://auth.stream-assistant-5.gekimoe.areatm.com/run/v3.0/", $"vender={vender}&game={gameCode}");
                if (get_auth != null && get_auth != "__Error__") break;

                updateUI("GEKImoe Stream Assistant 5 서버 접속 에러. 10초 후 재시도...");
                await Task.Delay(10000);
            }

            if (get_auth == "NotAuthorized")
            {
                return new AuthResult { IsSuccess = false, ErrorMessage = "인증 실패. AreaTM 라이선스를 확인하세요." };
            }

            updateUI("서버 인증 성공! (※ 2027년 1월 지원 종료 예정)");
            await Task.Delay(1000);

            // 2단계: 플랜(SW 버전) 확인
            updateUI("플랜 정보를 불러오는 중...");
            string plan = "";
            while (true)
            {
                plan = await PostDataAsync("https://auth.stream-assistant-5.gekimoe.areatm.com/run/v3.0/", $"mode=1&vender={vender}&game={gameCode}");
                if (plan != null && plan != "__Error__") break;

                updateUI("플랜 정보 수신 지연. 10초 후 재시도합니다...");
                await Task.Delay(10000);
            }

            // 성공 결과 반환
            return new AuthResult { IsSuccess = true, Plan = plan };
        }
    }
}