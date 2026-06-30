using UnityEngine;
using UnityEngine.UI;

// Aquest script està al Prefab de la cel·la
public class CellView : MonoBehaviour
{
    private Image baseImage;         // L'Image que canvia de color
    public GameObject crossOverlay; // Arrossega aquí l'objecte fill amb la "X"
    private CellData logicData;     // Referència a les dades lògiques

    private void Awake()
    {
        baseImage = GetComponent<Image>();
        if (crossOverlay != null) crossOverlay.SetActive(false);
    }

    // Aquesta funció enllaça la lògica amb la vista
    public void LinkLogic(CellData data, Color visualColor)
    {
        logicData = data;
        baseImage.color = visualColor;
    }

    public void OnCellClicked()
    {
        if (logicData == null) return;

        logicData.IsMarked = true;
        if (crossOverlay != null) crossOverlay.SetActive(true);

        Debug.Log($"{logicData.Color} Cell clicked");
    }
}