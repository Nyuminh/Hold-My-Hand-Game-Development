using UnityEngine;
using Unity.Netcode;
using Blocks.Gameplay.Core;

public class PlayerRoleVisuals : NetworkBehaviour
{
    [Header("Visual Models")]
    [Tooltip("Kéo GameObject chứa model Dad vào đây")]
    public GameObject dadModel;
    
    [Tooltip("Kéo GameObject chứa model Child vào đây")]
    public GameObject childModel;

    // Biến mạng lưu role của player này (True = Dad, False = Child)
    private NetworkVariable<bool> m_IsDad = new NetworkVariable<bool>(
        true, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            // Server sẽ quyết định Role cho nhân vật vừa spawn
            // Nếu đây là nhân vật của Host (ServerClientId), Role của nó sẽ = HostIsDad
            // Nếu là của Client, Role sẽ ngược lại (!HostIsDad)
            if (OwnerClientId == NetworkManager.ServerClientId)
            {
                m_IsDad.Value = Lobby3DManager.HostIsDad;
            }
            else
            {
                m_IsDad.Value = !Lobby3DManager.HostIsDad;
            }
        }

        // Lắng nghe sự kiện đổi role (nếu có)
        m_IsDad.OnValueChanged += OnRoleChanged;
        
        // Cập nhật giao diện ngay lập tức
        UpdateVisuals(m_IsDad.Value);
    }

    public override void OnNetworkDespawn()
    {
        m_IsDad.OnValueChanged -= OnRoleChanged;
        base.OnNetworkDespawn();
    }

    private void OnRoleChanged(bool previous, bool current)
    {
        UpdateVisuals(current);
    }

    private void UpdateVisuals(bool isDadActive)
    {
        if (dadModel != null) dadModel.SetActive(isDadActive);
        if (childModel != null) childModel.SetActive(!isDadActive);
    }
}
