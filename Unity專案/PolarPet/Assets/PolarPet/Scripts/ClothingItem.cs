using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 衣服道具：點擊場景中的道具（SpriteRenderer）後，打開UI物件面板
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ClothingItem : MonoBehaviour, IPointerClickHandler
{
    [Header("UI面板引用")]
    [Tooltip("衣服UI面板控制器")]
    [SerializeField] private ClothingUIPanel clothingUIPanel;

    [Header("外觀")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // 確保 Collider2D 存在，用於點擊檢測
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogWarning("ClothingItem: 需要 Collider2D 組件來進行點擊檢測！");
        }

        // 如果沒有指定 clothingUIPanel，嘗試在場景中查找
        if (clothingUIPanel == null)
        {
            clothingUIPanel = FindObjectOfType<ClothingUIPanel>();
        }
    }

    /// <summary>
    /// 點擊道具時打開UI面板
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (clothingUIPanel == null)
        {
            Debug.LogWarning("ClothingItem: ClothingUIPanel 未設置！");
            return;
        }

        // 打開UI面板
        clothingUIPanel.OpenPanel();
    }
}

