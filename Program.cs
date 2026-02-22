using System;

namespace GEKImoeStreamNTAutoStart
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // 💡 상황에 맞춰 원하는 클래스를 호출하면 됩니다.

            // 예시 1: 업데이터를 먼저 띄우고 싶을 때
            // var updater = new UpdaterApp();
            // updater.Run();

            // 예시 2: 평소처럼 오토스타트를 띄울 때
            var autoStart = new AutoStartApp();
            autoStart.Run();
        }
    }
}