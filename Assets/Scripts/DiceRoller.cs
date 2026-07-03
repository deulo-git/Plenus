using System.Collections;
using UnityEngine;

public class DiceRoller : MonoBehaviour
{
    [SerializeField] private Transform[] faceRotations;
    [SerializeField] private float rollDuration = 1.2f;

    private bool isRolling;

    public void Roll(int faceIndex)
    {
        if (isRolling)
            return;

        StartCoroutine(RollRoutine(faceIndex));
    }

    private IEnumerator RollRoutine(int faceIndex)
    {
        isRolling = true;

        Vector3 start = transform.localEulerAngles;
        Vector3 target = faceRotations[faceIndex].localEulerAngles;

        // Randomly choose whether each axis spins clockwise or counter-clockwise
        int rotX = Random.Range(4, 8) * (Random.value > 0.5f ? 1 : -1);
        int rotY = Random.Range(4, 8) * (Random.value > 0.5f ? 1 : -1);
        int rotZ = Random.Range(4, 8) * (Random.value > 0.5f ? 1 : -1);

        // Add extra full rotations before reaching the target orientation
        target.x += rotX * 360f;
        target.y += rotY * 360f;
        target.z += rotZ * 360f;

        float elapsed = 0f;

        while (elapsed < rollDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / rollDuration);

            // Ease Out Cubic: starts fast and gradually slows down
            t = 1f - Mathf.Pow(1f - t, 3);

            // Interpolate Euler angles to preserve all full rotations
            transform.localEulerAngles = Vector3.Lerp(start, target, t);

            yield return null;
        }

        // Ensure the die ends on the exact target face
        transform.localEulerAngles = faceRotations[faceIndex].localEulerAngles;
        isRolling = false;
    }
}