using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;
using Rhino.Collections;

using GH_IO;
using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Data;
using System.Drawing;
using System.Reflection;
using System.Collections;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;



/// <summary>
/// This class will be instantiated on demand by the Script component.
/// </summary>
public class Script_Instance : GH_ScriptInstance
{
#region Utility functions
  /// <summary>Print a String to the [Out] Parameter of the Script component.</summary>
  /// <param name="text">String to print.</param>
  private void Print(string text) { __out.Add(text); }
  /// <summary>Print a formatted String to the [Out] Parameter of the Script component.</summary>
  /// <param name="format">String format.</param>
  /// <param name="args">Formatting parameters.</param>
  private void Print(string format, params object[] args) { __out.Add(string.Format(format, args)); }
  /// <summary>Print useful information about an object instance to the [Out] Parameter of the Script component. </summary>
  /// <param name="obj">Object instance to parse.</param>
  private void Reflect(object obj) { __out.Add(GH_ScriptComponentUtilities.ReflectType_CS(obj)); }
  /// <summary>Print the signatures of all the overloads of a specific method to the [Out] Parameter of the Script component. </summary>
  /// <param name="obj">Object instance to parse.</param>
  private void Reflect(object obj, string method_name) { __out.Add(GH_ScriptComponentUtilities.ReflectType_CS(obj, method_name)); }
#endregion

#region Members
  /// <summary>Gets the current Rhino document.</summary>
  private RhinoDoc RhinoDocument;
  /// <summary>Gets the Grasshopper document that owns this script.</summary>
  private GH_Document GrasshopperDocument;
  /// <summary>Gets the Grasshopper script component that owns this script.</summary>
  private IGH_Component Component; 
  /// <summary>
  /// Gets the current iteration count. The first call to RunScript() is associated with Iteration==0.
  /// Any subsequent call within the same solution will increment the Iteration count.
  /// </summary>
  private int Iteration;
#endregion

  /// <summary>
  /// This procedure contains the user code. Input parameters are provided as regular arguments, 
  /// Output parameters as ref arguments. You don't have to assign output parameters, 
  /// they will have a default value.
  /// </summary>
  private void RunScript(Brep Cube, Curve Crv, int Seed, double SubdivDist, double SubdivRandomRate, double ReduceDist, double ReduceRandomRate, int Loops, ref object result)
  {
        //System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
    Random random = new Random();
    double maxVolume = BrepVolume(Cube);
    List<Brep> curCubes = DivideCube(Cube);
    List<Brep> newCubes = new List<Brep>();
    for(int i = 0; i < Loops; i++){
      //stopWatch.Start();
      if(i != 0){
        curCubes = new List<Brep>(newCubes);
      }
      newCubes.Clear();
      foreach(Rhino.Geometry.Brep cube in curCubes){
        double dist = distBrepCrv(cube, Crv);
        dist *= (random.NextDouble() * SubdivRandomRate) + (1 - SubdivRandomRate);
        if(dist < SubdivDist){
          newCubes.AddRange(DivideCube(cube));
        } else{
          newCubes.Add(cube);
        }
      }
      //stopWatch.Stop();
      //Print("Step 1 RunTime " + stopWatch.ElapsedMilliseconds.ToString());
      //stopWatch.Restart();
      List<Brep> reducedCubes = new List<Brep>();
      foreach(Rhino.Geometry.Brep cube in newCubes){
        double dist = distBrepCrv(cube, Crv);
        dist *= (random.NextDouble() * ReduceRandomRate) + (1 - ReduceRandomRate);
        dist *= System.Math.Pow((BrepVolume(cube) / maxVolume), 0.2);
        if(dist > ReduceDist){
          reducedCubes.Add(cube);
        }
      }
      newCubes = reducedCubes;
      //stopWatch.Stop();
      //Print("Step 2 RunTime " + stopWatch.ElapsedMilliseconds.ToString());
    }

    result = newCubes;

  }

  // <Custom additional code> 
    double distBrepCrv(Brep x, Curve Crv){
    double t;
    Crv.ClosestPoint(BrepCentroid(x), out t);
    Point3d ptOnCrv = Crv.PointAt(t);
    return ptOnCrv.DistanceTo(BrepCentroid(x));
  }

  double BrepVolume(Brep x){
    return Rhino.Geometry.VolumeMassProperties.Compute(x).Volume;
  }

  Point3d BrepCentroid(Brep x){
    return Rhino.Geometry.VolumeMassProperties.Compute(x).Centroid;
  }

  NurbsSurface Srf3Pt(Point3d A, Point3d B, Point3d C){
    Point3d D = new Point3d(0, 0, 0);
    D = Point3d.Add(D, (B + C - A));
    NurbsSurface srf = NurbsSurface.CreateFromCorners(A, B, D, C);
    return srf;
  }

  Brep Cube4Pt(Point3d A, Point3d B, Point3d C, Point3d D){
    List<Brep> srfs = new List<Brep>();

    Point3d E = new Point3d(0, 0, 0);
    Point3d F = new Point3d(0, 0, 0);
    Point3d G = new Point3d(0, 0, 0);

    E = Point3d.Add(E, (B + C - A));
    F = Point3d.Add(F, (B + D - A));
    G = Point3d.Add(G, (C + D - A));

    srfs.Add(Brep.TryConvertBrep(Srf3Pt(A, B, C)));
    srfs.Add(Brep.TryConvertBrep(Srf3Pt(A, B, D)));
    srfs.Add(Brep.TryConvertBrep(Srf3Pt(A, C, D)));
    srfs.Add(Brep.TryConvertBrep(Srf3Pt(D, F, G)));
    srfs.Add(Brep.TryConvertBrep(Srf3Pt(B, F, E)));
    srfs.Add(Brep.TryConvertBrep(Srf3Pt(C, E, G)));

    Brep[] Brs = Brep.JoinBreps(srfs, 0.01);
    return Brs[0];
  }

  List<Brep> DivideCube(Brep Cube){
    List<Brep> newCubes = new List<Brep>();
    int n = Cube.Vertices.Count();
    for (int i = 0; i < n; i++){
      int[] edgeIndexs = Cube.Vertices[i].EdgeIndices();
      int n1 = edgeIndexs.Length;
      List<Point3d> midPoints = new List<Point3d>();
      for (int j = 0; j < n1; j++){
        midPoints.Add(Cube.Edges[edgeIndexs[j]].PointAtNormalizedLength(0.5));
      }
      newCubes.Add(Cube4Pt(Cube.Vertices[i].Location, midPoints[0], midPoints[1], midPoints[2]));
    }
    return newCubes;
  }
  // </Custom additional code> 

  private List<string> __err = new List<string>(); //Do not modify this list directly.
  private List<string> __out = new List<string>(); //Do not modify this list directly.
  private RhinoDoc doc = RhinoDoc.ActiveDoc;       //Legacy field.
  private IGH_ActiveObject owner;                  //Legacy field.
  private int runCount;                            //Legacy field.
  
  public override void InvokeRunScript(IGH_Component owner, object rhinoDocument, int iteration, List<object> inputs, IGH_DataAccess DA)
  {
    //Prepare for a new run...
    //1. Reset lists
    this.__out.Clear();
    this.__err.Clear();

    this.Component = owner;
    this.Iteration = iteration;
    this.GrasshopperDocument = owner.OnPingDocument();
    this.RhinoDocument = rhinoDocument as Rhino.RhinoDoc;

    this.owner = this.Component;
    this.runCount = this.Iteration;
    this. doc = this.RhinoDocument;

    //2. Assign input parameters
        Brep Cube = default(Brep);
    if (inputs[0] != null)
    {
      Cube = (Brep)(inputs[0]);
    }

    Curve Crv = default(Curve);
    if (inputs[1] != null)
    {
      Crv = (Curve)(inputs[1]);
    }

    int Seed = default(int);
    if (inputs[2] != null)
    {
      Seed = (int)(inputs[2]);
    }

    double SubdivDist = default(double);
    if (inputs[3] != null)
    {
      SubdivDist = (double)(inputs[3]);
    }

    double SubdivRandomRate = default(double);
    if (inputs[4] != null)
    {
      SubdivRandomRate = (double)(inputs[4]);
    }

    double ReduceDist = default(double);
    if (inputs[5] != null)
    {
      ReduceDist = (double)(inputs[5]);
    }

    double ReduceRandomRate = default(double);
    if (inputs[6] != null)
    {
      ReduceRandomRate = (double)(inputs[6]);
    }

    int Loops = default(int);
    if (inputs[7] != null)
    {
      Loops = (int)(inputs[7]);
    }



    //3. Declare output parameters
      object result = null;


    //4. Invoke RunScript
    RunScript(Cube, Crv, Seed, SubdivDist, SubdivRandomRate, ReduceDist, ReduceRandomRate, Loops, ref result);
      
    try
    {
      //5. Assign output parameters to component...
            if (result != null)
      {
        if (GH_Format.TreatAsCollection(result))
        {
          IEnumerable __enum_result = (IEnumerable)(result);
          DA.SetDataList(1, __enum_result);
        }
        else
        {
          if (result is Grasshopper.Kernel.Data.IGH_DataTree)
          {
            //merge tree
            DA.SetDataTree(1, (Grasshopper.Kernel.Data.IGH_DataTree)(result));
          }
          else
          {
            //assign direct
            DA.SetData(1, result);
          }
        }
      }
      else
      {
        DA.SetData(1, null);
      }

    }
    catch (Exception ex)
    {
      this.__err.Add(string.Format("Script exception: {0}", ex.Message));
    }
    finally
    {
      //Add errors and messages... 
      if (owner.Params.Output.Count > 0)
      {
        if (owner.Params.Output[0] is Grasshopper.Kernel.Parameters.Param_String)
        {
          List<string> __errors_plus_messages = new List<string>();
          if (this.__err != null) { __errors_plus_messages.AddRange(this.__err); }
          if (this.__out != null) { __errors_plus_messages.AddRange(this.__out); }
          if (__errors_plus_messages.Count > 0) 
            DA.SetDataList(0, __errors_plus_messages);
        }
      }
    }
  }
}