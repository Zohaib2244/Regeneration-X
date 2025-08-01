using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;
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
public enum ReconstructionType
{
    Default,
    Spiral
}
public enum VOBYShape
{
    Sphere,
    Cube,
}
// Helper for serializing lists with JsonUtility
[System.Serializable]
public class Serialization<T>
{
    public List<T> items;
    public Serialization(List<T> items) { this.items = items; }
}
[System.Serializable]
public struct VOBTransformData
{
    public Vector3 position;
    public Quaternion rotation;
}