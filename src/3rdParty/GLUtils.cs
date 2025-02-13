﻿/*
MechJeb2  Copyright (C) 2013
This program comes with ABSOLUTELY NO WARRANTY!
This is free software, and you are welcome to redistribute it
under certain conditions, as outlined in the full content of
the GNU General Public License (GNU GPL), version 3, revision
date 29 June 2007.
*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Trajectories
{
    public static class GLUtils
    {
        static Material _material;
        static Material material
        {
            get
            {
                //_material ??= new Material(Shader.Find("KSP/Orbit Line"));
                if (_material == null)
                    _material = new Material(Shader.Find("KSP/Orbit Line"));

                // _material ??= new Material(Shader.Find("KSP/Particles/Additive"));       // fallback shader
                if (_material == null)
                    _material = new Material(Shader.Find("KSP/Particles/Additive"));       // fallback shader
                if (!_material)
                    Util.LogError("GLUtils material is null");
                return _material;
            }
        }

        public static void DrawMapViewGroundMarker(CelestialBody body, double latitude, double longitude, Color c,  double rotation = 0, double radius = 0)
        {
            DrawGroundMarker(body, latitude, longitude, c, true, rotation, radius);
        }

        public static void DrawGroundMarker(CelestialBody body, double latitude, double longitude, Color c, bool map, double rotation = 0, double radius = 0)
        {
            Vector3d up = body.GetSurfaceNVector(latitude, longitude);
            var height = body.pqsController.GetSurfaceHeight(QuaternionD.AngleAxis(longitude, Vector3d.down) * QuaternionD.AngleAxis(latitude, Vector3d.forward) * Vector3d.right);
            if (height < body.Radius) { height = body.Radius; }
            Vector3d center = body.position + height * up;

            Vector3d camPos = map ? ScaledSpace.ScaledToLocalSpace(PlanetariumCamera.Camera.transform.position) : (Vector3d)FlightCamera.fetch.mainCamera.transform.position;

            if (IsOccluded(center, body, camPos)) return;

            Vector3d north = Vector3d.Exclude(up, body.transform.up).normalized;

            if (radius <= 0) { radius = map ? body.Radius / 15 : 5; }

            if (!map)
            {
                Vector3 centerPoint = FlightCamera.fetch.mainCamera.WorldToViewportPoint(center);
                if (centerPoint.z < 0)
                    return;
            }

            GLTriangle(center, center + radius * (QuaternionD.AngleAxis(rotation - 10, up) * north),
                        center + radius * (QuaternionD.AngleAxis(rotation + 10, up) * north), c, map);
            GLTriangle(center, center + radius * (QuaternionD.AngleAxis(rotation + 110, up) * north),
                        center + radius * (QuaternionD.AngleAxis(rotation + 130, up) * north), c, map);
            GLTriangle(center, center + radius * (QuaternionD.AngleAxis(rotation - 110, up) * north),
                        center + radius * (QuaternionD.AngleAxis(rotation - 130, up) * north), c, map);
        }

        public static void GLTriangle(Vector3d worldVertices1, Vector3d worldVertices2, Vector3d worldVertices3, Color c, bool map)
        {
            try
            {
                GL.PushMatrix();
                material?.SetPass(0);
                GL.LoadOrtho();
                GL.Begin(GL.TRIANGLES);
                GL.Color(c);
                GLVertex(worldVertices1, map);
                GLVertex(worldVertices2, map);
                GLVertex(worldVertices3, map);
                GL.End();
                GL.PopMatrix();
            }
            catch{}
        }

        public static void GLVertex(Vector3d worldPosition, bool map = false)
        {
            Vector3 screenPoint = map ? PlanetariumCamera.Camera.WorldToViewportPoint(ScaledSpace.LocalToScaledSpace(worldPosition)) : FlightCamera.fetch.mainCamera.WorldToViewportPoint(worldPosition);
            GL.Vertex3(screenPoint.x, screenPoint.y, 0);
        }

        public static void GLPixelLine(Vector3d worldPosition1, Vector3d worldPosition2, bool map)
        {
            Vector3 screenPoint1, screenPoint2;
            if (map)
            {
                screenPoint1 = PlanetariumCamera.Camera.WorldToScreenPoint(ScaledSpace.LocalToScaledSpace(worldPosition1));
                screenPoint2 = PlanetariumCamera.Camera.WorldToScreenPoint(ScaledSpace.LocalToScaledSpace(worldPosition2));
            }
            else
            {
                screenPoint1 = FlightCamera.fetch.mainCamera.WorldToScreenPoint(worldPosition1);
                screenPoint2 = FlightCamera.fetch.mainCamera.WorldToScreenPoint(worldPosition2);
            }

            if (screenPoint1.z > 0 && screenPoint2.z > 0)
            {
                GL.Vertex3(screenPoint1.x, screenPoint1.y, 0);
                GL.Vertex3(screenPoint2.x, screenPoint2.y, 0);
            }
        }


        //Tests if byBody occludes worldPosition, from the perspective of the planetarium camera
        // https://cesiumjs.org/2013/04/25/Horizon-culling/
        public static bool IsOccluded(Vector3d worldPosition,  CelestialBody byBody, Vector3d camPos)
        {
            Vector3d VC = (byBody.position - camPos) / (byBody.Radius - 100);
            Vector3d VT = (worldPosition - camPos) / (byBody.Radius - 100);

            double VT_VC = Vector3d.Dot(VT, VC);

            // In front of the horizon plane
            if (VT_VC < VC.sqrMagnitude - 1) return false;

            return VT_VC * VT_VC / VT.sqrMagnitude > VC.sqrMagnitude - 1;
        }

        //If dashed = false, draws 0-1-2-3-4-5...
        //If dashed = true, draws 0-1 2-3 4-5...
        public static void DrawPath(CelestialBody mainBody, List<Vector3d> points, Color c, bool map, bool dashed = false)
        {
            try
            {
                GL.PushMatrix();
                material?.SetPass(0);
                GL.LoadPixelMatrix();
                GL.Begin(GL.LINES);
                GL.Color(c);

                Vector3d camPos = map
                                      ? ScaledSpace.ScaledToLocalSpace(PlanetariumCamera.Camera.transform.position)
                                      : (Vector3d) FlightCamera.fetch.mainCamera.transform.position;

                int step = (dashed ? 2 : 1);
                for (int i = 0; i < points.Count - 1; i += step)
                {
                    if (!IsOccluded(points[i], mainBody, camPos) && !IsOccluded(points[i + 1], mainBody, camPos))
                        GLPixelLine(points[i], points[i + 1], map);
                }

                GL.End();
                GL.PopMatrix();
            }
            catch { }
        }

#if false
        public static void DrawOrbit(Orbit o, Color c)
        {
            List<Vector3d> points = new List<Vector3d>();
            if (o.eccentricity < 1)
            {
                //elliptical orbits:
                for (int trueAnomaly = 0; trueAnomaly < 360; trueAnomaly += 1)
                {
                    points.Add(o.SwappedAbsolutePositionAtUT(o.TimeOfTrueAnomaly(trueAnomaly, 0)));
                }
                points.Add(points[0]); //close the loop
            }
            else
            {
                //hyperbolic orbits:
                for (int meanAnomaly = -1000; meanAnomaly <= 1000; meanAnomaly += 5)
                {
                    points.Add(o.SwappedAbsolutePositionAtUT(o.UTAtMeanAnomaly(meanAnomaly * UtilMath.Deg2Rad, 0)));
                }
            }

            DrawPath(o.referenceBody, points, c, true, false);
        }
#endif
        }
}
