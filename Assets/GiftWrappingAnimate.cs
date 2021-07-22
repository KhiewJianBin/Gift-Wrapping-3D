using UnityEngine;

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using Random = UnityEngine.Random;

public class GiftWrappingAnimate : MonoBehaviour
{
    [Range(4,50)]
    public int NumOfRandomPoints = 20;
    public MeshFilter meshFilter;

    List<Vector3> points = new List<Vector3>();
    List<Vector3> verts = new List<Vector3>();
    List<int> tris = new List<int>();

    bool isLogicPaused = true;
    bool UnpauseLogic() => !isLogicPaused;

    void Start()
    {
        for (int i = 0; i < NumOfRandomPoints; i++)
        {
            points.Add(Random.insideUnitSphere * 2);
        }

        StartCoroutine(GiftWrapping(points));
    }

    public bool autoStep = true;
    float LogicStepTimer = 0;
    public float LogicStepDuration = 0.1f;

    void Update()
    {
        if(autoStep)
        {
            //one per each LogicStepDuration time frame
            LogicStepTimer += Time.deltaTime;
            if (LogicStepTimer >= LogicStepDuration)
            {
                isLogicPaused = false;
                LogicStepTimer = 0;
            }
        }
        else
        {
            //one permouse click
            if (Input.GetMouseButtonUp(0))
            {
                isLogicPaused = false;
            }
        }

        UpdateMesh();
    }

    void UpdateMesh()
    {
        if (verts.Count == 0 || tris.Count == 0) return;

        if (meshFilter)
        {
            if (!meshFilter.sharedMesh) meshFilter.mesh = new Mesh();
            Mesh mesh = meshFilter.mesh;
            mesh.Clear(); //need to remeber to clear first,because setting verts,tris and normals some takes time
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
        }
    }
    
    public IEnumerator GiftWrapping(List<Vector3> points)
    {
        isLogicPaused = true;
        yield return new WaitUntil(UnpauseLogic);

        //1. Get the first Face that lies tangent to the convex hull , by first obtaining an edge on the hull,
        //lookthough all points, and checking to see which set of triangles(edge+point) is the CCW most or CW most rotating using the edge as the pivot
        GiftWrappingEdge firstedge = default;
        yield return FindEdgeOnHull(points,(newEdge)=> { firstedge = newEdge; });
        Vector3 r = default;
        yield return StartCoroutine(PivotOnEdge(firstedge, points, (newPoint) => { r = newPoint; }));
        Vector3[] firstFace = new Vector3[] { firstedge.StartPoint, firstedge.EndPoint, r };

        //2. once we got our first triangle/face, add it to our mesh, and queue the 3 edges.
        Stack<GiftWrappingEdge> Q = new Stack<GiftWrappingEdge>();
        GiftWrappingEdge edge1 = new GiftWrappingEdge(firstFace[1], firstFace[0]);//order matters, as it will affect when we add verts,and tris in drawing order
        GiftWrappingEdge edge2 = new GiftWrappingEdge(firstFace[2], firstFace[1]);
        GiftWrappingEdge edge3 = new GiftWrappingEdge(firstFace[0], firstFace[2]);
        Q.Push(edge1);
        Q.Push(edge2);
        Q.Push(edge3);

        verts.Add(firstFace[0]);
        verts.Add(firstFace[1]);
        verts.Add(firstFace[2]);
        tris.Add(0);
        tris.Add(1);
        tris.Add(2);

        EdgesToCheck = Q.ToList();
        isLogicPaused = true; yield return new WaitUntil(UnpauseLogic);

        List<string> processedEdges = new List<string>();

        //3. for each edges queued, we use one by one as the base edge, and run though PivotOnEdge,
        //to find which new third point, can be used with the base edge to construct a triangle that will lie on the convex hull
        while (Q.Count != 0)
        {
            GiftWrappingEdge edge = Q.Pop();
            if (NotProcessed(edge))
            {
                EdgesToCheck.Remove(edge);
                CurrentEdgeChecking = new GiftWrappingEdge(edge.StartPoint, edge.EndPoint);
                isLogicPaused = true; yield return new WaitUntil(UnpauseLogic);

                Vector3 q = default;
                yield return StartCoroutine(PivotOnEdge(edge, points,(newPoint)=> { q = newPoint; }));

                IncomingPoints = new List<Vector3>() { edge.StartPoint, edge.EndPoint, q };
                isLogicPaused = true; yield return new WaitUntil(UnpauseLogic);

                //4. when we found our third point / new triangle , just add it to the mesh
                //edge start & edge end is already part of our mesh verts list, check to see if our third point is already included else we add it as a new vert
                //Reuse verts by finding within verts list, - will slow down a generation, but will reduce mesh vert count
                int t1 = verts.IndexOf(edge.StartPoint);
                int t2 = verts.IndexOf(edge.EndPoint);
                int t3 = verts.IndexOf(q);
                if (t3 == -1)
                {
                    verts.Add(q);
                    t3 = verts.Count - 1;
                }
                //TODO: Optimize, there is a chance that the triangle already added, but the triangle we are added is just in different order. (adding them anyways for now)
                tris.Add(t1);
                tris.Add(t2);
                tris.Add(t3);

                //5. remeber the edge that we processed
                MarkProcessedEdges(edge);

                Vector3[] newFace = new Vector3[] { edge.StartPoint, edge.EndPoint, q };
                edge1 = new GiftWrappingEdge(newFace[1], newFace[0]);
                edge2 = new GiftWrappingEdge(newFace[2], newFace[1]);
                edge3 = new GiftWrappingEdge(newFace[0], newFace[2]);

                //6. check the edges of the new triangle/face if it's already processed, else queue it to process next loop
                if (NotProcessed(edge1)) Q.Push(edge1);
                if (NotProcessed(edge2)) Q.Push(edge2);
                if (NotProcessed(edge3)) Q.Push(edge3);

                AddedEdges.Add(edge);
                IncomingPoints.Clear();
                EdgesToCheck = Q.ToList();
                isLogicPaused = true; yield return new WaitUntil(UnpauseLogic);

                MarkProcessedEdges(edge);
            }
        }

        yield return null;

        bool NotProcessed(GiftWrappingEdge edge)
        {
            //Edge does not have a direction, so we check on both direction
            Vector3 v1 = edge.StartPoint;
            Vector3 v2 = edge.EndPoint;

            string id = $"{v1.x}{v1.y}{v1.z}{v2.x}{v2.y}{v2.z}";
            string id2 = $"{v2.x}{v2.y}{v2.z}{v1.x}{v1.y}{v1.z}";

            return !processedEdges.Contains(id) && !processedEdges.Contains(id2);
        }
        void MarkProcessedEdges(GiftWrappingEdge edge)
        {
            //Save the Edge as Start End Vector Point as a unique string ID
            Vector3 v1 = edge.StartPoint;
            Vector3 v2 = edge.EndPoint;
            string id = $"{v1.x}{v1.y}{v1.z}{v2.x}{v2.y}{v2.z}";
            processedEdges.Add(id);
        }

        IEnumerator FindEdgeOnHull(List<Vector3> P, Action<GiftWrappingEdge> callback)
        {
            //a. First get the left most,bottom most,back most point, this will be the start point of our edge
            Vector3 p = P[0];
            for (int index = 1; index < points.Count; index++)
            {
                if (points[index].x < p.x)
                {
                    p = points[index];
                }
                else if (points[index].x == p.x && points[index].y < p.y)
                {
                    p = points[index];
                }
                else if (points[index].x == p.x && points[index].y == p.y && points[index].z < p.z)
                {
                    p = points[index];
                }
            }
            IncomingPoints.Clear();
            IncomingPoints.Add(p);
            isLogicPaused = true; yield return new WaitUntil(UnpauseLogic);

            //b. loop though all points and check to see if we have any point that lies on the same YZ plane
            //not sure if this is needed, as most likely dont have a point, and even if there's a point,
            //from my understanding this only helps isolate and find the endpoint, but computational wise, it still goes though the entire point list anyways
            Vector3 q = p;
            foreach (Vector3 r in P)
            {
                if (q.z == r.z && q.y == r.y && q.x < r.x)
                {
                    q = r;
                }
            }

            //c. Created a temp point as the end point of the edge on the same YZ plane, any distance away 
            if (q == p)
            {
                q = p + new Vector3(2f, 0, 0);
            }

            IncomingPoints.Add(q);
            isLogicPaused = true; yield return new WaitUntil(UnpauseLogic);

            //d. Using the Edge, right though PivotOnEdge to check and obtain a third point, which is guaranteed to be on convex hull
            GiftWrappingEdge edge = new GiftWrappingEdge(p, q);
            yield return StartCoroutine(PivotOnEdge(edge, P, (newPoint) => { q = newPoint; }));
            callback(new GiftWrappingEdge(p, q));
        }
        //Complexity O(n)
        IEnumerator PivotOnEdge(GiftWrappingEdge edge, List<Vector3> P, Action<Vector3> callback)
        {
            //a. we first get any point to start constructing a triangle as our base of comparison
            //also record the unsigned area of the triangle - to be used later
            Vector3 p = P[0];
            double area2 = SquaredArea(edge.StartPoint, edge.EndPoint, p);

            CurrentEdgeChecking = new GiftWrappingEdge(edge.StartPoint, edge.EndPoint);
            IncomingPoints = new List<Vector3>() { p };
            isLogicPaused = true; yield return new WaitUntil(UnpauseLogic);

            originalTri = new List<Vector3>() { edge.StartPoint, edge.EndPoint, p };
            isLogicPaused = true; yield return new WaitUntil(UnpauseLogic);


            for (int i = 1; i < P.Count; i++)
            {
                //b. for point calculate if the point lies Left or Right of Triangle
                //this can be done by using the signedvolume of a tetrahedron
                double volume = SignedVolume(edge.StartPoint, edge.EndPoint, p, P[i]);
                currentTriangleColor = Color.blue;
                currentTriangle = new List<Vector3>() { edge.StartPoint, edge.EndPoint, P[i] };
                IncomingPoints = new List<Vector3>() { P[i] };
                isLogicPaused = true; yield return new WaitUntil(UnpauseLogic);

                //b. check and see what sides the point lies on the our triangle
                //checking volume < 0 or volume > 0, CCW/CW doesnt not matter and is depends on the order of inputs to SignedVolume?
                if (volume < 0)
                {
                    p = P[i];

                    currentTriangleColor = Color.green;
                    isLogicPaused = true; yield return new WaitUntil(UnpauseLogic);

                    nextTriangle = new List<Vector3>() { edge.StartPoint, edge.EndPoint, p }; 
                    isLogicPaused = true; yield return new WaitUntil(UnpauseLogic);
                }

                //c. if the volume is 0, means that all 4 points are co-planar (lies on the same triangle/plane) and does not create a tetrahedron
                // with that, we want to compare the new point with our original point to see which one creates the bigger triangle by just compare the area
                else if (volume == 0) 
                {
                    double _area2 = SquaredArea(edge.StartPoint, edge.EndPoint, P[i]);
                    if (_area2 > area2)
                    {
                        p = P[i];
                        area2 = _area2;

                        currentTriangleColor = Color.green;
                        isLogicPaused = true; yield return new WaitUntil(UnpauseLogic);

                        nextTriangle = new List<Vector3>() { edge.StartPoint, edge.EndPoint, p };
                        isLogicPaused = true; yield return new WaitUntil(UnpauseLogic);
                    }
                    else
                    {
                        currentTriangleColor = Color.red;
                        isLogicPaused = true; yield return new WaitUntil(UnpauseLogic);
                    }
                }
                else
                {
                    currentTriangleColor = Color.red;
                    isLogicPaused = true; yield return new WaitUntil(UnpauseLogic);
                }
            }

            originalTri.Clear();
            currentTriangle.Clear();
            nextTriangle.Clear();
            IncomingPoints.Clear();
            callback(p);
        }

        double SquaredArea(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            //Heron's Formula for the area of a triangle, use when you know length of triangle
            //p is half the perimeter, (a+b+c) /2
            //Area = sqrt(p * (p-a) * (p-b) * (p-c));

            double a = Vector3.Distance(p1, p2);
            double b = Vector3.Distance(p2, p3);
            double c = Vector3.Distance(p3, p1);

            double p = (a + b + c) / 2;
            double area = Math.Sqrt(p * (p - a) * (p - b) * (p - c));

            return area * area;
        }

        //SignedVolume are volumes that can be either positive or negative,
        //depending on the orientation in space of the region whose volume is being measured.
        double SignedVolume(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            //formular for calculating determinant
            return Vector3.Dot(a - d, Vector3.Cross(b - d, c - d)) / 6;
        }
    }




    List<Vector3> IncomingPoints = new List<Vector3>();
    List<GiftWrappingEdge> AddedEdges = new List<GiftWrappingEdge>();
    List<GiftWrappingEdge> EdgesToCheck = new List<GiftWrappingEdge>();
    GiftWrappingEdge CurrentEdgeChecking = null;
    List<Vector3> originalTri = new List<Vector3>();
    List<Vector3> currentTriangle = new List<Vector3>();
    Color currentTriangleColor = Color.blue;
    List<Vector3> nextTriangle = new List<Vector3>();

    /// <summary>
    /// White Sphere : Initial Points
    /// Green Sphere : Next Point To Consider (depedns on context)
    /// Red Line : Next Edge To Use as the base to Pivot The Triangle 
    /// White Line : Starting Triangle To Rotate
    /// Dark Green Line : Possible Next Triangle/Face that lies on the Hull
    /// Magenta Line : Initial Points 
    /// 
    /// </summary>
    void OnDrawGizmos()
    {
        foreach (Vector3 point in points)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(point, 0.05f);
        }
        foreach (Vector3 point in IncomingPoints)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(point, 0.1f);
        }
        foreach (GiftWrappingEdge edge in AddedEdges)
        {
            Gizmos.color = Color.grey;
            Gizmos.DrawLine(edge.StartPoint, edge.EndPoint);
        }
        foreach (GiftWrappingEdge edge in EdgesToCheck)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(edge.StartPoint, edge.EndPoint);
        }
        if (CurrentEdgeChecking != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(CurrentEdgeChecking.StartPoint, CurrentEdgeChecking.EndPoint);
        }
        if (originalTri.Count != 0)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(originalTri[0], originalTri[1]);
            Gizmos.DrawLine(originalTri[1], originalTri[2]);
            Gizmos.DrawLine(originalTri[2], originalTri[0]);
        }
        if (currentTriangle.Count != 0)
        {
            Gizmos.color = currentTriangleColor;
            Gizmos.DrawLine(currentTriangle[0], currentTriangle[1]);
            Gizmos.DrawLine(currentTriangle[1], currentTriangle[2]);
            Gizmos.DrawLine(currentTriangle[2], currentTriangle[0]);
        }
        if (nextTriangle.Count != 0)
        {
            Gizmos.color = new Color32(21, 71, 52, 255);
            Gizmos.DrawLine(nextTriangle[0], nextTriangle[1]);
            Gizmos.DrawLine(nextTriangle[1], nextTriangle[2]);
            Gizmos.DrawLine(nextTriangle[2], nextTriangle[0]);
        }
    }
}

public class GiftWrappingEdge
{
    public GiftWrappingEdge(Vector3 start, Vector3 end)
    {
        StartPoint = start;
        EndPoint = end;
    }
    public Vector3 StartPoint;
    public Vector3 EndPoint;
}