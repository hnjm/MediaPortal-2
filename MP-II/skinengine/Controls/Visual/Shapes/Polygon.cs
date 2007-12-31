#region Copyright (C) 2007 Team MediaPortal

/*
    Copyright (C) 2007 Team MediaPortal
    http://www.team-mediaportal.com
 
    This file is part of MediaPortal II

    MediaPortal II is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal II is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal II.  If not, see <http://www.gnu.org/licenses/>.
*/

#endregion
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using MediaPortal.Core.Properties;
using MediaPortal.Core.InputManager;
using SkinEngine;
using SkinEngine.DirectX;

using RectangleF = System.Drawing.RectangleF;
using PointF = System.Drawing.PointF;
using SizeF = System.Drawing.SizeF;
using Matrix = Microsoft.DirectX.Matrix;

using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace SkinEngine.Controls.Visuals
{
  public class Polygon : Shape
  {
    Property _pointsProperty;

    public Polygon()
    {
      Init();
    }

    public Polygon(Polygon s)
      : base(s)
    {
      Init();
      foreach (Point point in s.Points)
      {
        Points.Add(point);
      }
    }

    public override object Clone()
    {
      return new Polygon(this);
    }

    void Init()
    {
      _pointsProperty = new Property(new PointCollection());
    }


    public Property PointsProperty
    {
      get
      {
        return _pointsProperty;
      }
      set
      {
        _pointsProperty = value;
      }
    }

    /// <summary>
    /// Gets or sets the points.
    /// </summary>
    /// <value>The points.</value>
    public PointCollection Points
    {
      get
      {
        return (PointCollection)_pointsProperty.GetValue();
      }
      set
      {
        _pointsProperty.SetValue(value);
      }
    }


    /// <summary>
    /// Performs the layout.
    /// </summary>
    protected override void PerformLayout()
    {
      float cx, cy;
      Free();
      double w = Width; if (w == 0) w = ActualWidth;
      double h = Height; if (h == 0) h = ActualHeight;
      Vector3 orgPos = new Vector3(ActualPosition.X, ActualPosition.Y, ActualPosition.Z);
      //Fill brush
      if (Stroke == null || StrokeThickness <= 0)
      {
        ActualPosition = new Vector3(ActualPosition.X, ActualPosition.Y, 1);
        ActualWidth = w;
        ActualHeight = h;
      }
      else
      {
        ActualPosition = new Vector3((float)(ActualPosition.X + StrokeThickness), (float)(ActualPosition.Y + StrokeThickness), 1);
        ActualWidth = w - 2 * +StrokeThickness;
        ActualHeight = h - 2 * +StrokeThickness;
      }
      PositionColored2Textured[] verts;
      GraphicsPath path = GetPolygon(new RectangleF(ActualPosition.X, ActualPosition.Y, (float)ActualWidth, (float)ActualHeight), out cx, out cy);
      PointF[] vertices = ConvertPathToTriangleFan(path, (int)+(cx), (int)(cy));
      if (Fill != null)
      {
        _vertexBufferFill = new VertexBuffer(typeof(PositionColored2Textured), vertices.Length, GraphicsDevice.Device, Usage.WriteOnly, PositionColored2Textured.Format, Pool.Default);
        verts = (PositionColored2Textured[])_vertexBufferFill.Lock(0, 0);
        unchecked
        {
          for (int i = 0; i < vertices.Length; ++i)
          {
            verts[i].X = vertices[i].X;
            verts[i].Y = vertices[i].Y;
            verts[i].Z = 1.0f;
          }
        }
        Fill.SetupBrush(this, ref verts);
        _vertexBufferFill.Unlock();
        _verticesCountFill = (verts.Length - 2);
      }
      //border brush

      ActualPosition = new Vector3(orgPos.X, orgPos.Y, orgPos.Z);
      if (Stroke != null && StrokeThickness > 0)
      {
        ActualPosition = new Vector3(ActualPosition.X, ActualPosition.Y, 1);
        ActualWidth = w;
        ActualHeight = h;
        path = GetPolygon(new RectangleF(ActualPosition.X, ActualPosition.Y, (float)ActualWidth, (float)ActualHeight), out cx, out cy);
        vertices = ConvertPathToTriangleStrip(path, (int)(cx), (int)(cy), (float)StrokeThickness);

        _vertexBufferBorder = new VertexBuffer(typeof(PositionColored2Textured), vertices.Length, GraphicsDevice.Device, Usage.WriteOnly, PositionColored2Textured.Format, Pool.Default);
        verts = (PositionColored2Textured[])_vertexBufferBorder.Lock(0, 0);
        unchecked
        {
          for (int i = 0; i < vertices.Length; ++i)
          {
            verts[i].X = vertices[i].X;
            verts[i].Y = vertices[i].Y;
            verts[i].Z = 1.0f;
          }
        }
        Stroke.SetupBrush(this, ref verts);
        _vertexBufferBorder.Unlock();
        _verticesCountBorder = (verts.Length - 2);
      }

      ActualPosition = new Vector3(orgPos.X, orgPos.Y, orgPos.Z);
      ActualWidth = w;
      ActualHeight = h;
    }
    public override void Measure(Size availableSize)
    {
      base.Measure(availableSize);
      int width = 0;
      int height = 0;

      for (int i = 0; i < Points.Count; ++i)
      {
        Point p = (Point)Points[i];
        if (p.X > width) width = p.X;
        if (p.Y > height) height = p.Y;
      }

      _desiredSize = new System.Drawing.Size((int)Width, (int)Height);
      if (Width == 0)
        _desiredSize.Width = (int)width;
      if (Height == 0)
        _desiredSize.Height = (int)height;
      _desiredSize.Width += (int)(Margin.X + Margin.W);
      _desiredSize.Height += (int)(Margin.Y + Margin.Z);
      
    }
    #region Get the desired Rounded Rectangle path.
    private GraphicsPath GetPolygon(RectangleF baseRect, out float cx, out float cy)
    {
      Point[] points = new Point[Points.Count];
      for (int i = 0; i < Points.Count; ++i)
      {
        points[i] = (Point)Points[i];
        points[i].X += (int)baseRect.X;
        points[i].Y += (int)baseRect.Y;
      }
      CalcCentroid(points, out cx, out cy);
      GraphicsPath mPath = new GraphicsPath();
      mPath.AddPolygon(points);
      mPath.CloseFigure();
      mPath.Flatten();
      return mPath;
    }
    #endregion


    void ZCross(ref Point left, ref Point right, out double result)
    {
      result = left.X * right.Y - left.Y * right.X;
    }
    public void CalcCentroid(Point[] points, out float cx, out float cy)
    {
      Vector2 centroid = new Vector2();
      double temp;
      double area = 0;
      Point v1 = (Point)Points[points.Length - 1];
      Point v2;
      for (int index = 0; index < points.Length; ++index, v1 = v2)
      {
        v2 = (Point)points[index];
        ZCross(ref v1, ref v2, out temp);
        area += temp;
        centroid.X += (float)((v1.X + v2.X) * temp);
        centroid.Y += (float)((v1.Y + v2.Y) * temp);
      }
      temp = 1 / (Math.Abs(area) * 3);
      centroid.X *= (float)temp;
      centroid.Y *= (float)temp;

      cx = (float)centroid.X;
      cy = (float)centroid.Y;
    }
  }
}
