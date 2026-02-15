using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetPackage : MonoBehaviour
{
    [HideInInspector] public List<Compartment> compartments;
    public float moveSpeed = 2f;
    [HideInInspector] public GameObject compartmentPrefab;
    [HideInInspector] public float targetX = 2f;

    private const float SPACING = 2.4f;

    public Action onAllFilled;
    public Action onEnterComplete;

    public void Setup(List<CompartmentData> dataList)
    {
        compartments = new List<Compartment>();

        // Center compartments around 0: for 3 items → -1.3, 0, 1.3
        float offset = (dataList.Count - 1) * SPACING * 0.5f;

        for (int i = 0; i < dataList.Count; i++)
        {
            GameObject compObj = Instantiate(compartmentPrefab, transform);
            compObj.transform.localPosition = new Vector3(i * SPACING - offset, 0f, 0f);

            // Apply color material to second material slot
            var renderer = compObj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material[] mats = renderer.materials;
                if (mats.Length > 1)
                {
                    mats[1] = GameScript.Instance.AssignMaterial(dataList[i].color);
                    renderer.materials = mats;
                }
            }

            compartments.Add(new Compartment
            {
                color = dataList[i].color,
                _compartment = compObj,
                isFilled = false
            });
        }
    }

    public void Enter()
    {
        StartCoroutine(EnterRoutine());
    }

    private IEnumerator EnterRoutine()
    {
        yield return StartCoroutine(MoveX(-8f, targetX));
        onEnterComplete?.Invoke();
    }

    public void FillCompartment(ColorType color)
    {
        // First try exact color match, then first empty slot
        var comp = compartments.Find(c => c.color == color && !c.isFilled);
        if (comp == null)
            comp = compartments.Find(c => !c.isFilled);
        if (comp != null)
            comp.isFilled = true;

        if (IsAllFilled())
            Exit();
    }

    public bool IsAllFilled()
    {
        foreach (var comp in compartments)
        {
            if (!comp.isFilled)
                return false;
        }
        return true;
    }

    public Compartment GetCompartment(ColorType color)
    {
        // First try exact color match, then first empty slot
        var comp = compartments.Find(c => c.color == color && !c.isFilled);
        if (comp == null)
            comp = compartments.Find(c => !c.isFilled);
        return comp;
    }

    private void Exit()
    {
        StartCoroutine(MoveX(targetX, 8f, true));
    }

    private IEnumerator MoveX(float fromX, float toX, bool destroyAfter = false)
    {
        Vector3 pos = transform.position;
        pos.x = fromX;
        transform.position = pos;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * moveSpeed;
            float clamped = Mathf.Clamp01(t);
            // Ease-in-out: fast start, slow middle, fast end
            float eased = clamped * clamped * (3f - 2f * clamped);
            pos.x = Mathf.Lerp(fromX, toX, eased);
            transform.position = pos;
            yield return null;
        }

        pos.x = toX;
        transform.position = pos;

        if (destroyAfter)
        {
            onAllFilled?.Invoke();
            Destroy(gameObject, 0.1f);
        }
    }
}

[System.Serializable]
public class Compartment
{
    public ColorType color;
    public GameObject _compartment;
    public bool isFilled;
}

[System.Serializable]
public class TargetPackageData
{
    public List<CompartmentData> compartments;
}

[System.Serializable]
public class CompartmentData
{
    public ColorType color;
}
