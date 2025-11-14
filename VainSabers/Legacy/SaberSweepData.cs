using UnityEngine;
using VainSabers.Helpers;

namespace VainSabers.Legacy;

internal class SaberSweepData
{
    private const int ExpectedFPS = 120;

    private struct SaberSweepDataElement
    {
        public Vector3 Position;
        public Vector3 BladeDirection;
        public Vector3 TipDeltaPosition;
        public float ElapsedTime;

        public SaberSweepDataElement(Vector3 position, Vector3 bladeDirection, Vector3 tipDeltaPosition, float elapsedTime)
        {
            Position = position;
            BladeDirection = bladeDirection;
            TipDeltaPosition = tipDeltaPosition;
            ElapsedTime = elapsedTime;
        }
    }
    private readonly CircularBuffer<SaberSweepDataElement> _sweepData;

    public SaberSweepData(int historyLength)
    {
        _sweepData = new CircularBuffer<SaberSweepDataElement>(historyLength);
    }

    public void AddData(Vector3 position, Vector3 bladeDirection, float elapsedTime, out Vector3 tipVelocity)
    {
        Vector3 tipDeltaPosition = Vector3.zero;
        if (_sweepData.Count > 0)
        {
            var prevElement = _sweepData[0];
            Vector3 tipPos = position + bladeDirection;
            Vector3 prevTipPos = prevElement.Position + prevElement.BladeDirection;
            tipDeltaPosition = tipPos - prevTipPos;
        }

        var newElement = new SaberSweepDataElement(position, bladeDirection, tipDeltaPosition, elapsedTime);
        tipVelocity = GetTipVel(newElement);
        _sweepData.Add(newElement);
    }

    private Vector3 GetTipVel(SaberSweepDataElement element)
    {
        if (element.ElapsedTime < 0.0001f)
            return element.TipDeltaPosition.normalized;
        return element.TipDeltaPosition / element.ElapsedTime;
    }

    public void GetDataPointAtTimeAgo(float tAgo, out Vector3 position, out Vector3 bladeDirection, out Vector3 tipVelocity)
    {
        position = Vector3.zero;
        bladeDirection = Vector3.forward;
        tipVelocity = Vector3.zero;

        if (_sweepData.Count == 0)
            return;

        float accumulatedTime = 0f;
        SaberSweepDataElement current = _sweepData[0];
        if (tAgo <= 0f)
        {
            position = current.Position;
            bladeDirection = current.BladeDirection;
            tipVelocity = GetTipVel(current);
            return;
        }

        for (int i = 1; i < _sweepData.Count; i++)
        {
            SaberSweepDataElement prev = _sweepData[i];
            accumulatedTime += current.ElapsedTime;

            if (accumulatedTime >= tAgo)
            {
                float overshoot = accumulatedTime - tAgo;
                float segmentDuration = current.ElapsedTime;
                float lerpFactor = (segmentDuration > 0f) ? 1f - (overshoot / segmentDuration) : 0f;

                position = Vector3.Lerp(current.Position, prev.Position, lerpFactor);
                bladeDirection = Vector3.Lerp(current.BladeDirection, prev.BladeDirection, lerpFactor).normalized;
                tipVelocity = Vector3.Lerp(GetTipVel(current), GetTipVel(prev), lerpFactor);
                return;
            }

            current = prev;
        }
        
        position = current.Position;
        bladeDirection = current.BladeDirection;
        tipVelocity = GetTipVel(current);
    }

    public float GetBladeTipSpeed()
    {
        if (_sweepData.Count < 2)
            return 0f;

        int pointsToUse = Mathf.Min(4, _sweepData.Count);
        float totalDistance = 0f;
        float totalTime = 0f;

        for (int i = 0; i < pointsToUse - 1; i++)
        {
            var current = _sweepData[i];
            var prev = _sweepData[i + 1];

            Vector3 p0 = current.Position + current.BladeDirection;
            Vector3 p1 = prev.Position + prev.BladeDirection;
            float dt = current.ElapsedTime;

            if (dt > 0f)
            {
                totalDistance += Vector3.Distance(p0, p1);
                totalTime += dt;
            }
        }

        return Mathf.Sqrt(totalTime > 0.001f ? totalDistance / totalTime : 0f);
    }
}