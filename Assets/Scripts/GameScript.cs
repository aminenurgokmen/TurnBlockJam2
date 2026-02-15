using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.Unity.VisualStudio.Editor;
using UnityEngine;

public class GameScript : MonoBehaviour
{
    public float moveSpeed = 5f;
    public static GameScript Instance;
    public Material[] materials;
    public GameObject blockPrefab;

    public List<Target> targets;
    public List<TargetScript> targetScripts;
    public List<ColorType> levelColors;
    public GameObject matchParticlePrefab;
    public GameObject wholeBlockPrefab;

    public Transform xPosition;
    public float wholeBlockLerpSpeed = 2f;

    public List<TargetPackageData> targetPackageDatas;
    public GameObject targetPackagePrefab;
    public GameObject compartmentPrefab;
    public Transform packageMidpoint;
    private TargetPackage activeTargetPackage;
    private int currentPackageIndex = 0;

    [Header("Slot List")]
    public List<SlotScript> slotList;

    public void SpawnMatchParticle(Vector3 pos)
    {
        if (matchParticlePrefab != null)
        {
            GameObject particle = Instantiate(matchParticlePrefab, pos + Vector3.up * 0.5f, Quaternion.identity);
            Destroy(particle, 2f);
        }
        GetComponent<AudioSource>().Play();
    }
    private void Awake()
    {
        Instance = this;
    }
    void Start()
    {
            SetupTargets();
            SpawnNextPackage();
    }
    public void SetupTargets()
    {
        foreach (var item in CanvasManager.Instance.targetScripts)
        {
            item.gameObject.SetActive(false);
        }

        for (int i = 0; i < targets.Count; i++)
        {

            CanvasManager.Instance.targetScripts[i].gameObject.SetActive(true);
            CanvasManager.Instance.targetScripts[i].Setup(targets[i].color, targets[i].count);
            targetScripts.Add(CanvasManager.Instance.targetScripts[i]);

        }

    }

    private SlotScript GetFirstEmptySlot()
    {
        return slotList.Find(s => !s.isOccupied);
    }

    private List<SlotScript> pendingSlotTransfers = new List<SlotScript>();

    private void TransferSlotsToPackage()
    {
        if (activeTargetPackage == null) return;

        pendingSlotTransfers.Clear();
        foreach (var slot in slotList)
        {
            if (slot.isOccupied)
                pendingSlotTransfers.Add(slot);
        }

        SendNextSlotToPackage();
    }

    private void SendNextSlotToPackage()
    {
        if (activeTargetPackage == null) return;

        while (pendingSlotTransfers.Count > 0)
        {
            SlotScript slot = pendingSlotTransfers[0];
            pendingSlotTransfers.RemoveAt(0);

            if (!slot.isOccupied) continue;

            var comp = activeTargetPackage.compartments.Find(
                c => c.color == slot.storedColor && !c.isFilled);
            if (comp != null && comp._compartment != null)
            {
                GridManager.Instance.wholeBlockWaitCount++;
                GameObject wholeBlock = Instantiate(wholeBlockPrefab,
                    slot.transform.position, Quaternion.identity);
                if (wholeBlock.transform.childCount > 0)
                    wholeBlock.transform.GetChild(0).GetComponent<MeshRenderer>().material =
                        AssignMaterial(slot.storedColor);

                WholeBlockMover mover = wholeBlock.AddComponent<WholeBlockMover>();
                mover.parentTransform = comp._compartment.transform;
                mover.speed = wholeBlockLerpSpeed;
                mover.fillColor = slot.storedColor;
                mover.ownerPackage = activeTargetPackage;
                mover.onArrived = () => SendNextSlotToPackage();

                if (slot.storedBlock != null)
                    Destroy(slot.storedBlock);
                slot.storedBlock = null;
                slot.isOccupied = false;
                return; // wait for this one to arrive before sending next
            }
        }
    }

    public Material AssignMaterial(ColorType color)
    {
        return materials[(int)color];
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Break();
        }
        {

        }

    }

    public void SpawnNextPackage()
    {
        if (currentPackageIndex >= targetPackageDatas.Count)
            return;

        GameObject pkgObj = Instantiate(targetPackagePrefab, xPosition.position, xPosition.rotation);
        activeTargetPackage = pkgObj.GetComponent<TargetPackage>();
        activeTargetPackage.compartmentPrefab = compartmentPrefab;
        activeTargetPackage.Setup(targetPackageDatas[currentPackageIndex].compartments);
        activeTargetPackage.targetX = packageMidpoint != null ? packageMidpoint.position.x : 2f;
        activeTargetPackage.onAllFilled = () =>
        {
            currentPackageIndex++;
            SpawnNextPackage();
        };
        activeTargetPackage.onEnterComplete = () =>
        {
            TransferSlotsToPackage();
        };
        activeTargetPackage.Enter();
    }

    public void Collected(Material mat, Vector3 pos)
    {
        int colorIdx = System.Array.IndexOf(materials, mat);
        Debug.Log($"Collected: {mat.name} at {pos} + Color Index: {colorIdx}");
        TargetScript targetScript = targetScripts.Find(x => x.targetColor == colorIdx);

        if (targetScript && targetScript.IsCompleted())
        {
            return;
        }
        if (targets.Count == 1 && targets[0].color == -1 &&  !targetScripts[0].IsCompleted())
        {
            targetScript = targetScripts[0];

        }

        if (targetScript != null)
        {
            if (wholeBlockPrefab != null)
            {
                ColorType collectedColor = (ColorType)colorIdx;

                // Check if color exists in active package
                bool colorInPackage = false;
                Transform dest = xPosition;
                if (activeTargetPackage != null && colorIdx >= 0)
                {
                    var comp = activeTargetPackage.compartments.Find(
                        c => c.color == collectedColor && !c.isFilled);
                    if (comp != null && comp._compartment != null)
                    {
                        colorInPackage = true;
                        dest = comp._compartment.transform;
                    }
                }

                GridManager.Instance.wholeBlockWaitCount++;
                GameObject wholeBlock = Instantiate(wholeBlockPrefab, pos, Quaternion.identity);
                if (wholeBlock.transform.childCount > 0)
                    wholeBlock.transform.GetChild(0).GetComponent<MeshRenderer>().material = mat;

                if (colorInPackage)
                {
                    // Goes to compartment
                    WholeBlockMover mover = wholeBlock.AddComponent<WholeBlockMover>();
                    mover.parentTransform = dest;
                    mover.speed = wholeBlockLerpSpeed;
                    mover.targetScript = targetScript;
                    mover.fillColor = collectedColor;
                    mover.ownerPackage = activeTargetPackage;
                }
                else
                {
                    // Goes to slot list (waiting area)
                    SlotScript slot = GetFirstEmptySlot();
                    if (slot != null)
                    {
                        // Reserve slot immediately
                        slot.isOccupied = true;
                        slot.storedColor = collectedColor;
                        slot.storedBlock = wholeBlock;

                        WholeBlockMover mover = wholeBlock.AddComponent<WholeBlockMover>();
                        mover.parentTransform = slot.settlePoint != null ? slot.settlePoint : slot.transform;
                        mover.speed = wholeBlockLerpSpeed;
                        mover.targetScript = targetScript;
                        mover.fillColor = collectedColor;
                        mover.endY = 0.8f;
                    }
                }
            }
            else
            {
                targetScript.Collect();
            }
        }

    }
    [System.Serializable]
    public class Target
    {
        public int color;
        public int count;
    }

}
