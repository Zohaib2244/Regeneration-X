using UnityEngine;
using DG.Tweening;
public static class NuttyUtilities
{
    public static void TriggerSlomo(float duration = 0.5f, float transitionTime = 0.5f, float targetTimeScale = 0.5f)
    {
        float originalFixedDeltaTime = Time.fixedDeltaTime;
        float originalTimeScale = Time.timeScale;

        DOTween.Kill("SlomoTween");

        DOTween.To(() => Time.timeScale, x =>
        {
            Time.timeScale = x;
            Time.fixedDeltaTime = originalFixedDeltaTime * (Time.timeScale / originalTimeScale);
        }, targetTimeScale, transitionTime)
            .SetEase(Ease.InOutSine)
            .SetId("SlomoTween")
            .OnComplete(() =>
            {
                DOVirtual.DelayedCall(duration, () =>
                {
                    DOTween.To(() => Time.timeScale, x =>
                    {
                        Time.timeScale = x;
                        Time.fixedDeltaTime = originalFixedDeltaTime * (Time.timeScale / originalTimeScale);
                    }, originalTimeScale, transitionTime)
                        .SetEase(Ease.InOutSine)
                        .SetId("SlomoTween")
                        .OnComplete(() => Time.fixedDeltaTime = originalFixedDeltaTime);
                });
            });
    }
}