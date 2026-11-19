using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Vùng Chiến Thắng & Kết Thúc Màn Chơi trên Khinh Khí Cầu Zephyr (Zone 8).
    /// Khi cả hai cha con (hoặc 1 người khi chơi thử solo) bước lên boong tàu khinh khí cầu,
    /// trigger này sẽ kích hoạt pháo hoa ăn mừng, dialog chiến thắng và hoàn thành màn chơi!
    /// </summary>
    public class ZephyrVictoryClimaxManager : NetworkBehaviour
    {
        public static ZephyrVictoryClimaxManager Instance { get; private set; }

        [Header("Celebration FX")]
        [SerializeField] private ParticleSystem[] victoryFireworks;
        [SerializeField] private AudioSource victoryMusicAudio;

        [Header("Dialogue")]
        [SerializeField] private string dadSpeaker = "Ba";
        [SerializeField, TextArea] private string dadVictoryLine = "Mình làm được rồi con ơi! Lên khinh khí cầu rồi ba con mình về nhà thôi!";
        [SerializeField] private float dialogueDuration = 5.0f;

        private bool isVictoryTriggered = false;
        private readonly HashSet<ulong> playersOnBoard = new HashSet<ulong>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isVictoryTriggered) return;

            var movement = other.GetComponent<CoreMovement>() ?? other.GetComponentInParent<CoreMovement>();
            if (movement != null)
            {
                ulong clientId = movement.NetworkObject != null ? movement.NetworkObject.OwnerClientId : 0;
                playersOnBoard.Add(clientId);

                // Nếu solo hoặc mạng có đủ 2 người bước vào boong tàu (hoặc ít nhất 1 người khi solo)
                TriggerVictoryCelebration();
            }
        }

        public void TriggerVictoryCelebration()
        {
            if (isVictoryTriggered) return;
            isVictoryTriggered = true;

            Debug.Log("[ZephyrVictoryClimaxManager] 🎉 VICTORY! Hai cha con đã lên khinh khí cầu Zephyr thành công!");

            // 1. Pháo hoa ăn mừng
            if (victoryFireworks != null)
            {
                foreach (var ps in victoryFireworks)
                {
                    if (ps != null) ps.Play();
                }
            }

            // 2. Nhạc chiến thắng
            if (victoryMusicAudio != null) victoryMusicAudio.Play();

            // 3. Thoại cảm xúc
            if (PlayScene3ObjectiveDirector.Instance != null)
            {
                PlayScene3ObjectiveDirector.Instance.ShowDialog(dadSpeaker, dadVictoryLine, dialogueDuration);
            }
        }
    }
}
