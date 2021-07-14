using UnityEngine;

public class NodeMarker : MonoBehaviour
{
    private Renderer _renderer;
    public Color PrimaryColor { get; set; } = Color.white;

    private void Awake() => _renderer = GetComponentInChildren<Renderer>();

    public void SetColor(Color color) 
    {
        _renderer.material.color = color;
    }

    public void ResetColor() => _renderer.material.color = PrimaryColor;
}