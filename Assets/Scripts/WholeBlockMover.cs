using UnityEngine;

public class WholeBlockMover : MonoBehaviour
{
    public Transform parentTransform;
    public float speed = 2f;
    public TargetScript targetScript;
    public float waitDuration = 0.1f;
    public float startY = 3f;
    public float endY = 1.5f;
    public ColorType fillColor;
    public TargetPackage ownerPackage;
    public System.Action onArrived;

    private float t = 0f;
    private float waitTimer;
    private Vector3 startLocalPos;
    private Quaternion startLocalRot;
    private bool waiting = true;
    private bool reachedTop = false;

    void Start()
    {
        // Immediately parent it
        transform.SetParent(parentTransform);
        startLocalPos = transform.localPosition;
        startLocalRot = transform.localRotation;
        waitTimer = waitDuration;
    }

    void Update()
    {
        if (waiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                waiting = false;
                GridManager.Instance.wholeBlockWaitCount--;
            }
            return;
        }

        if (!reachedTop)
        {
            // Lerp from spawn local pos to (0, startY, 0)
            t += Time.deltaTime * speed;
            Vector3 target = new Vector3(0f, startY, 0f);
            transform.localPosition = Vector3.Lerp(startLocalPos, target, t);
            transform.localRotation = Quaternion.Slerp(startLocalRot, Quaternion.identity, t);

            if (t >= 1f)
            {
                transform.localPosition = target;
                transform.localRotation = Quaternion.identity;
                reachedTop = true;
                t = 0f;
            }
            return;
        }

        // Lerp y from startY down to endY
        t += Time.deltaTime * speed;
        float y = Mathf.Lerp(startY, endY, t);
        transform.localPosition = new Vector3(0f, y, 0f);

        if (t >= 1f)
        {
            transform.localPosition = new Vector3(0f, endY, 0f);
            // Unparent only for waiting slots (not package compartments)
            if (ownerPackage == null)
                transform.SetParent(null);
            if (targetScript != null)
                targetScript.Collect();
            if (ownerPackage != null)
            {
                if (parentTransform.childCount > 0)
                {
                    var child0 = parentTransform.GetChild(0);
                    var ps = child0.GetComponent<ParticleSystem>();
                    if (ps != null)
                        ps.Play();
                }
                if (parentTransform.childCount > 1)
                {
                    var child1 = parentTransform.GetChild(1);
                    child1.gameObject.SetActive(true);
                    var parentRenderer = parentTransform.GetComponent<MeshRenderer>();
                    var child1Renderer = child1.GetComponent<MeshRenderer>();
                    if (parentRenderer != null && child1Renderer != null)
                    {
                        Material[] parentMats = parentRenderer.materials;
                        if (parentMats.Length > 1)
                            child1Renderer.material = parentMats[1];
                    }
                }
                parentTransform.GetComponent<AudioSource>()?.Play();    
                ownerPackage.FillCompartment(fillColor);
            }
            onArrived?.Invoke();
            Destroy(this);
        }
    }
}
