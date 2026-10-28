using UnityEngine;
using UnityEngine.EventSystems;

// Each joystick owns one pointer; other fingers may rotate or throw independently.
public sealed class VirtualStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerExitHandler
{
    public RectTransform Knob;
    public Vector2 Value { get; private set; }
    int? pointer;
    RectTransform Area => (RectTransform)transform;
    public void OnPointerDown(PointerEventData e)
    {
        if (pointer.HasValue) return;
        pointer = e.pointerId; OnDrag(e);
    }
    public void OnDrag(PointerEventData e)
    {
        if (pointer != e.pointerId) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Area, e.position, e.pressEventCamera, out var p)) return;
        float radius = Mathf.Min(Area.rect.width, Area.rect.height) * .36f;
        Vector2 raw = Vector2.ClampMagnitude(p / Mathf.Max(radius, 1), 1);
        Value = raw.magnitude < .12f ? Vector2.zero : raw;
        Knob.anchoredPosition = raw * radius;
    }
    public void OnPointerUp(PointerEventData e) { if (pointer == e.pointerId) Release(); }
    public void OnPointerExit(PointerEventData e) { if (pointer == e.pointerId) Release(); }
    public void Release() { pointer = null; Value = Vector2.zero; if (Knob) Knob.anchoredPosition = Vector2.zero; }
    void OnDisable() => Release();
    void OnApplicationFocus(bool focus) { if (!focus) Release(); }
    void OnApplicationPause(bool pause) { if (pause) Release(); }
}
