namespace Framework
{
    /// <summary>StaticBatching 의 LateUpdate 러너 (숨김, 씬 전환 유지). 할 일이 없으면 스스로 꺼진다.</summary>
    internal sealed class StaticBatchRunner : BaseBehaviour
    {
        private void LateUpdate()
        {
            StaticBatching.Tick();
            if (StaticBatching.HasWork == false)
            {
                enabled = false;
            }
        }
    }
}
