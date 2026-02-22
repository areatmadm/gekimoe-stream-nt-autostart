using Photino.NET;
using System;

namespace GEKImoeStreamUpdaterNT
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                // 업데이터 실행
                UpdateApp app = new UpdateApp();
                app.Run();
            }
            catch (Exception ex)
            {
                // 💡 프로그램이 뻗을 때 바탕화면이나 현재 폴더에 사망 원인을 기록합니다!
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "updater_crash_log.txt");
                File.WriteAllText(logPath, $"[업데이터 크래시 로그]\r\n시간: {DateTime.Now}\r\n오류 내용:\r\n{ex.ToString()}");
            }
        }
    }
}