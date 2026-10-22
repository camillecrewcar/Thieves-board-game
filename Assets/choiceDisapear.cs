using UnityEngine;
using TMPro;
public class choiceDisapear : MonoBehaviour
{
    public MapController mapController;
    private SpriteRenderer spriteRenderer;
    public TextMeshProUGUI mylabel;
    private string originalText;
    

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalText = mylabel.text;
    }

    void Update()
    {
        if (mapController == null || spriteRenderer == null) return;

        if (mapController.Phase == 2){
            spriteRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 0f);
            mylabel.text = "";
        } else{
            spriteRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 1f);
            if(mapController.stealTime) {
                mylabel.text = "ekwipunek okradanego gracza";
            }else{
                mylabel.text = originalText;
            }
        }

    }
}