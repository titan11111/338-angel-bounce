using UnityEngine;
using UnityEngine.EventSystems;

public sealed class HoldDirection : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Vector2 Direction;
    public bool Held { get; private set; }
    int? pointer;
    public void OnPointerDown(PointerEventData e) { if (!pointer.HasValue) { pointer = e.pointerId; Held = true; } }
    public void OnPointerUp(PointerEventData e) { if (pointer == e.pointerId) Release(); }
    public void OnPointerExit(PointerEventData e) { if (pointer == e.pointerId) Release(); }
    public void Release() { pointer = null; Held = false; }
    void OnDisable() { Release(); }
}
