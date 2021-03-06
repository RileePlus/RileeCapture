﻿//----------------------------------------------------------------------------
//  Copyright (C) 2014 by Richard Lee. All rights reserved.
//  http://richardxlee.com
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Structure;
using Emgu.CV.Util;

namespace RileeCapture
{
   public static class SURFMatch
   {
      public static void FindMatch(Image<Gray, Byte> modelImage, Image<Gray, byte> observedImage, out long matchTime, out VectorOfKeyPoint modelKeyPoints, out VectorOfKeyPoint observedKeyPoints, out Matrix<int> indices, out Matrix<byte> mask, out HomographyMatrix homography)
      {
          int k = 2;
          double uniquenessThreshold = 0.8;
          SURFDetector surfCPU = new SURFDetector(500, false);
          Stopwatch watch;
          homography = null;

          //extract features from the object image
          modelKeyPoints = new VectorOfKeyPoint();
          Matrix<float> modelDescriptors = surfCPU.DetectAndCompute(modelImage, null, modelKeyPoints);

          watch = Stopwatch.StartNew();

          // extract features from the observed image
          observedKeyPoints = new VectorOfKeyPoint();
          Matrix<float> observedDescriptors = surfCPU.DetectAndCompute(observedImage, null, observedKeyPoints);
          BruteForceMatcher<float> matcher = new BruteForceMatcher<float>(DistanceType.L2);
          matcher.Add(modelDescriptors);

          indices = new Matrix<int>(observedDescriptors.Rows, k);
          using (Matrix<float> dist = new Matrix<float>(observedDescriptors.Rows, k))
          {
              matcher.KnnMatch(observedDescriptors, indices, dist, k, null);
              mask = new Matrix<byte>(dist.Rows, 1);
              mask.SetValue(255);
              Features2DToolbox.VoteForUniqueness(dist, uniquenessThreshold, mask);
          }

          int nonZeroCount = CvInvoke.cvCountNonZero(mask);
          if (nonZeroCount >= 4)
          {
              nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(modelKeyPoints, observedKeyPoints, indices, mask, 1.5, 20);
              if (nonZeroCount >= 4)
                 homography = Features2DToolbox.GetHomographyMatrixFromMatchedFeatures(modelKeyPoints, observedKeyPoints, indices, mask, 2);
              }
          watch.Stop();

          matchTime = watch.ElapsedMilliseconds;
      }

      /// <summary>
      /// Draw the model image and observed image, the matched features and homography projection.
      /// </summary>
      /// <param name="modelImage">The model image</param>
      /// <param name="observedImage">The observed image</param>
      /// <param name="matchTime">The output total time for computing the homography matrix.</param>
      /// <returns>The model image and observed image, the matched features and homography projection.</returns>
      public static Image<Bgr, Byte> Draw(List<Image<Bgr, Byte>> lmodelImage, Image<Bgr, byte> observedImage, out long matchTime)
      {
          HomographyMatrix homography;
          VectorOfKeyPoint modelKeyPoints;
          VectorOfKeyPoint observedKeyPoints;
          Matrix<int> indices;
          Matrix<byte> mask;
          matchTime = 0;

          Image<Bgr, Byte> result = observedImage;

          for (int i = 0; i < lmodelImage.Count; i++)
          {
              Image<Gray, Byte> modelImage_grey = lmodelImage[i].Convert<Gray, Byte>();
              Image<Gray, Byte> observedImage_grey = observedImage.Convert<Gray, Byte>();
              long matchTime_single = 0;

              FindMatch(modelImage_grey, observedImage_grey, out matchTime_single, out modelKeyPoints, out observedKeyPoints, out indices, out mask, out homography);

              //Draw the matched keypoints
              if (lmodelImage.Count == 1){
                  result = Features2DToolbox.DrawMatches(lmodelImage[0], modelKeyPoints, observedImage, observedKeyPoints,
                     indices, new Bgr(255, 255, 255), new Bgr(255, 255, 255), mask, Features2DToolbox.KeypointDrawType.DEFAULT);
              }

              #region draw the projected region on the image
              if (homography != null)
              {  //draw a rectangle along the projected model
                  Rectangle rect = lmodelImage[i].ROI;
                  PointF[] pts = new PointF[] { 
                    new PointF(rect.Left, rect.Bottom),
                    new PointF(rect.Right, rect.Bottom),
                    new PointF(rect.Right, rect.Top),
                    new PointF(rect.Left, rect.Top)};
                  homography.ProjectPoints(pts);

                  if (i==0)
                      result.DrawPolyline(Array.ConvertAll<PointF, Point>(pts, Point.Round), true, new Bgr(Color.Red), 2);
                  else if (i==1)
                      result.DrawPolyline(Array.ConvertAll<PointF, Point>(pts, Point.Round), true, new Bgr(Color.Yellow), 2);
                  else if (i==2)
                      result.DrawPolyline(Array.ConvertAll<PointF, Point>(pts, Point.Round), true, new Bgr(Color.Green), 2);
                  else if (i==3)
                      result.DrawPolyline(Array.ConvertAll<PointF, Point>(pts, Point.Round), true, new Bgr(Color.Blue), 2);
                  else if (i==4)
                      result.DrawPolyline(Array.ConvertAll<PointF, Point>(pts, Point.Round), true, new Bgr(Color.Purple), 2);
              }
              #endregion

              matchTime += matchTime_single;
          }

          return result;
      }
   }
}

