using UnityEngine;

/// <summary>
/// Cancels out scale inherited from an ancestor UI element.
///
/// Canvas Scaler's "Scale With Screen Size" mode makes the Canvas's runtime
/// transform.localScale track actual screen resolution vs. the reference
/// resolution (it only ever does this at runtime - in the Editor's Scene
/// view, and whenever the game happens to run at exactly the reference
/// resolution, that scale factor is 1, which is why hand-tuned numbers
/// "look right" there and nowhere else). Plain (non-RectTransform) objects
/// nested under that Canvas - like a 3D dice rig rendered through its own
/// camera into a RenderTexture - inherit that scale too, so they shrink or
/// grow with whatever resolution the game actually runs at, even though
/// they were never meant to be UI-scaled at all.
///
/// Attach this to the root of such a rig (e.g. DiceManager, parent of
/// DiceCamera and the dice meshes). Every frame it measures the parent's
/// current lossyScale and applies the exact inverse as this object's own
/// localScale, so the whole subtree always renders at the size/framing it
/// was originally authored at, regardless of screen resolution.
/// </summary>
[DefaultExecutionOrder(-100)]
public class CanvasScaleCompensator : MonoBehaviour
{
    private void Awake() => Compensate();
    private void OnEnable() => Compensate();

    // Canvas Scaler recalculates its scale factor reactively (screen size
    // changes, window resize, etc.), so keep re-checking cheaply rather than
    // computing this once.
    private void LateUpdate() => Compensate();

    private void Compensate()
    {
        Transform parent = transform.parent;
        if (parent == null) return;

        Vector3 parentLossy = parent.lossyScale;
        transform.localScale = new Vector3(
            InverseOrOne(parentLossy.x),
            InverseOrOne(parentLossy.y),
            InverseOrOne(parentLossy.z)
        );
    }

    private static float InverseOrOne(float value)
    {
        return Mathf.Approximately(value, 0f) ? 1f : 1f / value;
    }
}
