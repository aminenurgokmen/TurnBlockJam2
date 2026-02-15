using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using JetBrains.Annotations;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;
    public int width = 5;
    public int height = 5;
    public float cellSize = 1f;

    private Dictionary<Vector2Int, ColorType> colorGrid = new Dictionary<Vector2Int, ColorType>();
    public List<Block> allBlocks = new List<Block>();

    public bool isMatchProcessing = false;
    private bool isMoving = false;
    public int wholeBlockWaitCount = 0;

    public bool IsGridFrozen => isMatchProcessing || wholeBlockWaitCount > 0;



    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void RegisterBlock(Block block)
    {
        if (!allBlocks.Contains(block))
            allBlocks.Add(block);
        UpdateColorGridFromAll();
    }

    void Update()
    {
        if (!IsGridFrozen)
            ApplyGravityToEmptyBlocks();
    }




    public void UpdateColorGridFromAll()
    {
        colorGrid.Clear();
        foreach (var block in allBlocks)
        {
            var positions = block.GetOccupiedGridPositions();
            for (int i = 0; i < positions.Length; i++)
            {
                colorGrid[positions[i]] = block.blockData[i].color;
            }
        }
        // CheckAndSpawnAtTopIfEmpty(GameScript.Instance.blockPrefab);
    }

    public void UpdateColorGrid(Block block)
    {
        foreach (var pos in block.GetOccupiedGridPositions())
        {
            if (colorGrid.ContainsKey(pos))
                colorGrid.Remove(pos);
        }

        var positions = block.GetOccupiedGridPositions();
        for (int i = 0; i < positions.Length; i++)
        {
            colorGrid[positions[i]] = block.blockData[i].color;
        }
    }

    public void CheckForMatches()
    {
        // === Faz 1: Tüm tek-renk blok eşleşmelerini bul ===
        List<Block> singleColorMatches = new List<Block>();
        foreach (var block in allBlocks)
        {
            if (IsSingleColor(block))
                singleColorMatches.Add(block);
        }

        // === Faz 2: Tüm 2x2 eşleşmelerini bul ===
        List<(List<Vector2Int> positions, HashSet<Block> blocks, Material mat, Vector3 center)> matches2x2
            = new List<(List<Vector2Int>, HashSet<Block>, Material, Vector3)>();

        for (int x = 0; x < width - 1; x++)
        {
            for (int y = 0; y < height - 1; y++)
            {
                Vector2Int a = new Vector2Int(x, y);
                Vector2Int b = new Vector2Int(x + 1, y);
                Vector2Int c = new Vector2Int(x, y + 1);
                Vector2Int d = new Vector2Int(x + 1, y + 1);

                if (!colorGrid.ContainsKey(a) || !colorGrid.ContainsKey(b) ||
                    !colorGrid.ContainsKey(c) || !colorGrid.ContainsKey(d))
                    continue;

                ColorType ca = colorGrid[a], cb = colorGrid[b], cc = colorGrid[c], cd = colorGrid[d];
                if (!(ca == cb && cb == cc && cc == cd))
                    continue;

                List<Vector2Int> matchPositions = new List<Vector2Int> { a, b, c, d };
                HashSet<Block> involvedBlocks = new HashSet<Block>();

                foreach (var block in allBlocks)
                {
                    var occupiedPositions = block.GetOccupiedGridPositions();
                    foreach (var pos in occupiedPositions)
                    {
                        if (matchPositions.Contains(pos))
                        {
                            involvedBlocks.Add(block);
                            break;
                        }
                    }
                }

                if (involvedBlocks.Count != 2)
                    continue;

                // Zaten tek-renk olarak silinecek blokları atla
                bool skip = false;
                foreach (var bl in involvedBlocks)
                {
                    if (singleColorMatches.Contains(bl)) { skip = true; break; }
                }
                if (skip) continue;

                Material matchMaterial = null;
                foreach (var block in involvedBlocks)
                {
                    var positions = block.GetOccupiedGridPositions();
                    for (int i = 0; i < positions.Length; i++)
                    {
                        if (matchPositions.Contains(positions[i]))
                        {
                            matchMaterial = block.blockData[i].part.GetComponent<MeshRenderer>().sharedMaterial;
                            break;
                        }
                    }
                    if (matchMaterial != null) break;
                }

                Vector3 center = new Vector3((a.x * cellSize) + 1, 0, (a.y * cellSize) + 1);
                matches2x2.Add((matchPositions, involvedBlocks, matchMaterial, center));
            }
        }

        // === Hiç eşleşme yoksa sadece gravity uygula ===
        if (singleColorMatches.Count == 0 && matches2x2.Count == 0)
        {
            ApplyGravityToEmptyBlocks();
            return;
        }

        // === Faz 3: Tüm tek-renk eşleşmelerini aynı anda işle ===
        foreach (var block in singleColorMatches)
        {
            Material matchMaterial = block.blockData[0].part.GetComponent<MeshRenderer>().sharedMaterial;
            var allPositions = block.GetOccupiedGridPositions();
            Vector3 worldPos = Vector3.zero;
            foreach (var p in allPositions)
                worldPos += new Vector3(p.x * cellSize, 0, p.y * cellSize);
            worldPos /= allPositions.Length;
            GameScript.Instance.Collected(matchMaterial, worldPos);
            GameScript.Instance.SpawnMatchParticle(worldPos);

            var positions = block.GetOccupiedGridPositions();
            foreach (var pos in positions)
            {
                if (colorGrid.ContainsKey(pos))
                    colorGrid.Remove(pos);
            }
            foreach (var partData in block.blockData)
            {
                Destroy(partData.part);
            }
            allBlocks.Remove(block);
            Destroy(block.gameObject);
        }

        // === Faz 4: Tüm 2x2 eşleşmelerini aynı anda işle ===
        if (matches2x2.Count > 0)
        {
            // Silinecek tüm pozisyonları topla
            HashSet<Vector2Int> allMatchPositions = new HashSet<Vector2Int>();
            foreach (var match in matches2x2)
            {
                foreach (var pos in match.positions)
                    allMatchPositions.Add(pos);
            }

            // Her eşleşme için efekt ve UI
            foreach (var match in matches2x2)
            {
                Debug.Log($"✅ SIMULTANEOUS MATCH at {string.Join(", ", match.positions)}");
                GameScript.Instance.Collected(match.mat, match.center);
                GameScript.Instance.SpawnMatchParticle(match.center);
            }

            // Union-Find ile birleşme gruplarını oluştur
            Dictionary<Block, Block> parent = new Dictionary<Block, Block>();

            Block Find(Block b)
            {
                if (!parent.ContainsKey(b)) parent[b] = b;
                while (parent[b] != b) { parent[b] = parent[parent[b]]; b = parent[b]; }
                return b;
            }

            void Union(Block ba, Block bb)
            {
                Block ra = Find(ba), rb = Find(bb);
                if (ra != rb) parent[rb] = ra;
            }

            HashSet<Block> allInvolved = new HashSet<Block>();
            foreach (var match in matches2x2)
            {
                Block[] blks = new Block[match.blocks.Count];
                match.blocks.CopyTo(blks);
                for (int i = 1; i < blks.Length; i++)
                    Union(blks[0], blks[i]);
                foreach (var bl in match.blocks)
                    allInvolved.Add(bl);
            }

            // Eşleşen parçaları tüm bloklardan aynı anda sil
            foreach (var block in allInvolved)
            {
                var positions = block.GetOccupiedGridPositions();
                for (int i = block.blockData.Count - 1; i >= 0; i--)
                {
                    if (i < positions.Length && allMatchPositions.Contains(positions[i]))
                    {
                        if (colorGrid.ContainsKey(positions[i]))
                            colorGrid.Remove(positions[i]);
                        GameObject part = block.blockData[i].part;
                        block.blockData.RemoveAt(i);
                        Destroy(part);
                    }
                }
            }

            // Boş kalan blokları temizle
            List<Block> emptyBlocks = new List<Block>();
            foreach (var block in allInvolved)
            {
                if (block != null && block.blockData.Count == 0)
                    emptyBlocks.Add(block);
            }
            foreach (var block in emptyBlocks)
            {
                allBlocks.Remove(block);
                Destroy(block.gameObject);
            }

            UpdateColorGridFromAll();

            // Union-Find'dan birleşme gruplarını çıkar
            Dictionary<Block, List<Block>> groups = new Dictionary<Block, List<Block>>();
            foreach (var block in allInvolved)
            {
                if (block == null || block.blockData.Count == 0) continue;
                Block root = Find(block);
                if (!groups.ContainsKey(root))
                    groups[root] = new List<Block>();
                if (block != root)
                    groups[root].Add(block);
            }

            List<(Block main, List<Block> others)> mergeGroups = new List<(Block, List<Block>)>();
            foreach (var kvp in groups)
            {
                if (kvp.Key == null || kvp.Key.blockData.Count == 0) continue;
                List<Block> validOthers = new List<Block>();
                foreach (var other in kvp.Value)
                {
                    if (other != null && other.blockData.Count > 0)
                        validOthers.Add(other);
                }
                if (validOthers.Count > 0)
                    mergeGroups.Add((kvp.Key, validOthers));
            }

            if (mergeGroups.Count > 0)
            {
                isMatchProcessing = true;
                StartCoroutine(MoveAndTransferAll(mergeGroups, 0.2f));
            }
            else
            {
                StartCoroutine(ProcessNextMatchAfterDelay());
            }
        }
        else
        {
            // Sadece tek-renk eşleşmeleri vardı
            StartCoroutine(ProcessNextMatchAfterDelay());
        }
    }

    public IEnumerator WaitForGridSettleAndCheckMatches()
    {
        // Gravity işleminin başlaması için biraz bekle
        yield return new WaitForSeconds(0.05f);
        
        // isMoving ve isSpawning flagları false olana kadar bekle (tüm işlemler bitsin diye)
        float maxWait = 5f;
        float elapsed = 0f;
        
        while ((isMoving || isMatchProcessing) && elapsed < maxWait)
        {
            yield return new WaitForSeconds(0.05f);
            elapsed += 0.05f;
        }
        
        // Grid stabilize olsun
        yield return new WaitForSeconds(0.2f);
        
        // Şimdi match kontrol et
        CheckForMatches();
    }

    private IEnumerator ProcessNextMatchAfterDelay()
    {
        // WholeBlock bekleme süresi bitene kadar bekle
        while (wholeBlockWaitCount > 0)
            yield return null;

        yield return new WaitForSeconds(0.3f);
        UpdateColorGridFromAll();
        // Gravity'yi beklemeden direk match kontrol et
        CheckForMatches();
    }


    private IEnumerator MoveAndTransferAll(List<(Block main, List<Block> others)> mergeGroups, float duration)
    {
        // WholeBlock bekleme süresi bitene kadar bekle
        while (wholeBlockWaitCount > 0)
            yield return null;

        yield return new WaitForSeconds(0.3f);

        // Bekleme sırasında yok edilmiş blokları filtrele
        mergeGroups.RemoveAll(g => g.main == null);
        foreach (var group in mergeGroups)
            group.others.RemoveAll(o => o == null);
        mergeGroups.RemoveAll(g => g.others.Count == 0);

        if (mergeGroups.Count == 0)
        {
            isMatchProcessing = false;
            ApplyGravityToEmptyBlocks();
            yield break;
        }

        // Tüm collider'ları kapat
        foreach (var group in mergeGroups)
        {
            group.main.SetColliderActive(false);
            foreach (var other in group.others)
                other.SetColliderActive(false);
        }

        // Tüm birleşme animasyonlarını aynı anda başlat
        float elapsed = 0f;
        Dictionary<Block, Vector3> startPositions = new Dictionary<Block, Vector3>();
        Dictionary<Block, Vector3> targetPositions = new Dictionary<Block, Vector3>();

        foreach (var group in mergeGroups)
        {
            foreach (var other in group.others)
            {
                startPositions[other] = other.transform.position;
                targetPositions[other] = group.main.transform.position;
            }
        }

        while (elapsed < duration)
        {
            foreach (var kvp in startPositions)
            {
                if (kvp.Key != null)
                    kvp.Key.transform.position = Vector3.Lerp(kvp.Value, targetPositions[kvp.Key], elapsed / duration);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Parçaları ana bloğa transfer et ve diğerlerini sil
        foreach (var group in mergeGroups)
        {
            if (group.main == null) continue;
            foreach (var other in group.others)
            {
                if (other == null) continue;
                other.transform.position = group.main.transform.position;

                foreach (var partData in other.blockData)
                {
                    partData.part.transform.SetParent(group.main.transform);
                    group.main.blockData.Add(partData);
                }
                other.blockData.Clear();
                allBlocks.Remove(other);
                Destroy(other.gameObject);
            }
            group.main.SetColliderActive(true);
        }

        UpdateColorGridFromAll();
        isMatchProcessing = false;
        ApplyGravityToEmptyBlocks();

        // Birleşen bloklarda tekrar tek-renk oluştuysa kontrol et
        foreach (var group in mergeGroups)
        {
            if (group.main != null && IsSingleColor(group.main))
            {
                CheckForMatches();
                yield break;
            }
        }
    }

    private bool IsSingleColor(Block block)
    {
        if (block.blockData.Count == 0)
            return false;

        ColorType firstColor = block.blockData[0].color;
        foreach (var data in block.blockData)
        {
            if (data.color != firstColor)
                return false;
        }
        return true;
    }

    public Dictionary<Vector2Int, ColorType> GetColorGrid()
    {
        return new Dictionary<Vector2Int, ColorType>(colorGrid);
    }



    //Kayma durumları cart curt

    public List<(Vector2Int[], string)> GetBlockStates()
    {
        List<(Vector2Int[], string)> result = new List<(Vector2Int[], string)>();

        for (int x = 0; x < width - 1; x += 2)
        {
            for (int y = 0; y < height - 1; y += 2)
            {
                Vector2Int a = new Vector2Int(x, y);
                Vector2Int b = new Vector2Int(x, y + 1);
                Vector2Int c = new Vector2Int(x + 1, y + 1);
                Vector2Int d = new Vector2Int(x + 1, y);

                bool hasA = colorGrid.ContainsKey(a);
                bool hasB = colorGrid.ContainsKey(b);
                bool hasC = colorGrid.ContainsKey(c);
                bool hasD = colorGrid.ContainsKey(d);

                string state = (hasA && hasB && hasC && hasD) ? "Dolu" :
                               (!hasA && !hasB && !hasC && !hasD) ? "Boş" : "Karışık";

                result.Add((new Vector2Int[] { a, b, c, d }, state));
            }
        }
        return result;
    }

    public void ApplyGravityToEmptyBlocks()
    {
        var states = GetBlockStates();

        // Önce dikey kaydırma
        foreach (var block in states)
        {
            if (block.Item2 == "Boş")
            {
                Vector2Int[] coords = block.Item1;
                int minY = coords[0].y;
                int x1 = coords[0].x;
                int x2 = coords[2].x;

                // Yalnızca dikey boşluk
                for (int y = minY + 2; y < height; y += 2)
                {
                    Vector2Int checkA = new Vector2Int(x1, y);
                    Vector2Int checkB = new Vector2Int(x1, y + 1);
                    Vector2Int checkC = new Vector2Int(x2, y + 1);
                    Vector2Int checkD = new Vector2Int(x2, y);

                    if (colorGrid.ContainsKey(checkA) || colorGrid.ContainsKey(checkB) ||
                        colorGrid.ContainsKey(checkC) || colorGrid.ContainsKey(checkD))
                    {
                        StartCoroutine(MoveBlockDownSmooth(
                            new Vector2Int[] { checkA, checkB, checkC, checkD },
                            cellSize * 2, .1f));
                    }
                }
            }
        }

        // Sonra sadece en alt satırda yatay kaydırma
        foreach (var block in states)
        {
            if (block.Item2 == "Boş")
            {
                Vector2Int[] coords = block.Item1;
                int minY = coords[0].y;
                int minX = coords[0].x;

                // Sadece en alt satır
                if (minY == 0)
                {
                    Vector2Int rightA = new Vector2Int(minX + 2, 0);
                    Vector2Int rightB = new Vector2Int(minX + 2, 1);
                    Vector2Int rightC = new Vector2Int(minX + 3, 1);
                    Vector2Int rightD = new Vector2Int(minX + 3, 0);

                    if (colorGrid.ContainsKey(rightA) || colorGrid.ContainsKey(rightB) ||
                        colorGrid.ContainsKey(rightC) || colorGrid.ContainsKey(rightD))
                    {
                        StartCoroutine(MoveBlockSidewaysSmooth(
                            new Vector2Int[] { rightA, rightB, rightC, rightD },
                            -cellSize * 2, 0.1f));
                    }
                }
            }
        }

        UpdateColorGridFromAll();

        // DebugBlockStates();
    }


    private IEnumerator MoveBlockDownSmooth(Vector2Int[] positions, float moveDistance, float duration)
    {
        if (isMoving || IsGridFrozen) yield break;

        isMoving = true;

        yield return new WaitForSeconds(0.05f);

        List<Block> blocksToMove = new List<Block>();

        foreach (var block in allBlocks)
        {
            var occupied = block.GetOccupiedGridPositions();
            bool matches = false;
            foreach (var pos in occupied)
            {
                if (System.Array.Exists(positions, p => p == pos))
                {
                    matches = true;
                    break;
                }
            }
            if (matches)
                blocksToMove.Add(block);
        }

        float elapsed = 0f;
        Vector3 moveVector = new Vector3(0, 0, -moveDistance);

        Dictionary<Block, Vector3> startPositions = new Dictionary<Block, Vector3>();
        foreach (var b in blocksToMove)
        {
            startPositions[b] = b.transform.position;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            foreach (var b in blocksToMove)
            {
                if (b == null) continue;
                b.transform.position = Vector3.Lerp(startPositions[b], startPositions[b] + moveVector, t);
            }

            yield return null;
        }

        foreach (var b in blocksToMove)
        {
            if (b == null) continue;
            b.transform.position = startPositions[b] + moveVector;
        }

        UpdateColorGridFromAll();
        isMoving = false;

        if (!IsGridFrozen)
            CheckForMatches();
    }


    private IEnumerator MoveBlockSidewaysSmooth(Vector2Int[] positions, float moveDistance, float duration)
    {
        if (isMoving || IsGridFrozen) yield break;

        isMoving = true;

        yield return new WaitForSeconds(0.05f);

        List<Block> blocksToMove = new List<Block>();

        foreach (var block in allBlocks)
        {
            var occupied = block.GetOccupiedGridPositions();
            bool matches = false;
            foreach (var pos in occupied)
            {
                if (System.Array.Exists(positions, p => p == pos))
                {
                    matches = true;
                    break;
                }
            }
            if (matches)
                blocksToMove.Add(block);
        }

        float elapsed = 0f;
        Vector3 moveVector = new Vector3(moveDistance, 0, 0);

        Dictionary<Block, Vector3> startPositions = new Dictionary<Block, Vector3>();
        foreach (var b in blocksToMove)
        {
            startPositions[b] = b.transform.position;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            foreach (var b in blocksToMove)
            {
                if (b == null) continue;
                b.transform.position = Vector3.Lerp(startPositions[b], startPositions[b] + moveVector, t);
            }

            yield return null;
        }

        foreach (var b in blocksToMove)
        {
            if (b == null) continue;
            b.transform.position = startPositions[b] + moveVector;
        }

        UpdateColorGridFromAll();
        isMoving = false;

        if (!IsGridFrozen)
            CheckForMatches(); // Kayma sonrası yeni match kontrolü
    }

}