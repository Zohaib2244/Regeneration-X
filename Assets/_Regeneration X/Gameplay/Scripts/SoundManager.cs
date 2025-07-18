using UnityEngine;

public class SoundManager : MonoBehaviour
{
    #region Singleton
    public static SoundManager Instance;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion
    public AudioSource _audioSource1;
    public AudioSource _audioSource2;
    public AudioClip _explosionSound;
    public AudioClip _blockfallSound;
    public AudioClip _reconstructionSound;

    public void PlayBlockFallSound()
    {
        if (_audioSource2 != null && _blockfallSound != null)
        {
            _audioSource2.clip = _blockfallSound;
            _audioSource2.Play();
        }
    }

    public void PlayExplosionSound()
    {
        PlayClip(_explosionSound);
    }

    public void PlayReconstructionSound()
    {
        PlayClip(_reconstructionSound);
    }

    void PlayClip(AudioClip clip)
    {
        if (_audioSource1 != null && clip != null)
        {
            _audioSource1.clip = clip;
            _audioSource1.Play();
        }
    }
}