using Photino.NET;
using System;
using System.Runtime.InteropServices;

namespace GEKImoeStreamUpdaterNT
{
    public static class WindowHelper
    {
        // 윈도우 모서리 제어를 위한 API 선언
        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

        // 아무 Photino 창이나 던져주면 모서리를 둥글게 깎아주는 함수
        public static void ApplyRoundedCorners(PhotinoWindow win)
        {
            // 💡 Windows 11 (빌드 22000 이상) 인지 확인합니다.
            if (Environment.OSVersion.Version.Build < 22000)
            {
                // Windows 10 이하에서는 둥근 모서리를 지원하지 않으므로 그냥 리턴합니다.
                return;
            }

            try
            {
                var hwnd = win.WindowHandle;
                int attribute = 33; // DWMWA_WINDOW_CORNER_PREFERENCE
                int preference = 2; // DWMWCP_ROUND (둥글게)

                DwmSetWindowAttribute(hwnd, attribute, ref preference, sizeof(int));
            }
            catch
            {
                // 만에 하나 발생할 에러를 위해 예외 처리
            }
        }
    }
}