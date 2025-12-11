using UnityEngine;

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
    
    // circular buffer
    private readonly SaberSweepDataElement[] m_sweepData;
    private int m_nextAddIndex = 0;
    private int m_elementCount = 0;

    public SaberSweepData(int historyLength)
    {
        m_sweepData = new SaberSweepDataElement[historyLength];
    }
    public void AddData(Vector3 position, Vector3 bladeDirection, float elapsedTime, out Vector3 tipVelocity)
    {
        Vector3 tipDeltaPosition = Vector3.zero;
        if (m_elementCount > 0)
        {
            var prevIndex = (m_nextAddIndex - 1 + m_sweepData.Length) % m_sweepData.Length;
            var prevElement = m_sweepData[prevIndex];
            
            Vector3 tipPos = position + bladeDirection;
            Vector3 prevTipPos = prevElement.Position + prevElement.BladeDirection;
            
            tipDeltaPosition = tipPos - prevTipPos;
        }
        
        var newElement = new SaberSweepDataElement(position, bladeDirection, tipDeltaPosition, elapsedTime);
        tipVelocity = GetTipVel(newElement);
        m_sweepData[m_nextAddIndex] = newElement;
        
        m_nextAddIndex = (m_nextAddIndex + 1) % m_sweepData.Length;
        m_elementCount++;
        if (m_elementCount > m_sweepData.Length)
            m_elementCount = m_sweepData.Length;
    }

    private Vector3 GetTipVel(SaberSweepDataElement element)
    {
        if (element.ElapsedTime < 0.0001f)
            return element.TipDeltaPosition.normalized;
        return element.TipDeltaPosition / element.ElapsedTime;
    }

    // fill given arrays with interpolated data
    public void GetDataPointAtTimeAgo(float tAgo, out Vector3 position, out Vector3 bladeDirection, out Vector3 tipVelocity)
    {
        position = Vector3.zero;
        bladeDirection = Vector3.forward;
        tipVelocity = Vector3.zero;
        
        if (m_elementCount == 0)
            return;

        float accumulatedTime = 0f;

        // Start from the most recent element
        int idx = (m_nextAddIndex - 1 + m_sweepData.Length) % m_sweepData.Length;
        SaberSweepDataElement current = m_sweepData[idx];

        // If asking for tAgo == 0, just return most recent
        if (tAgo <= 0f)
        {
            position = current.Position;
            bladeDirection = current.BladeDirection;
            tipVelocity = GetTipVel(current);
            return;
        }

        // Walk backwards through history
        for (int i = 1; i < m_elementCount; i++)
        {
            int prevIdx = (idx - 1 + m_sweepData.Length) % m_sweepData.Length;
            SaberSweepDataElement prev = m_sweepData[prevIdx];
            
            accumulatedTime += current.ElapsedTime;

            if (accumulatedTime >= tAgo)
            {
                // Find interpolation factor
                float overshoot = accumulatedTime - tAgo;
                float segmentDuration = current.ElapsedTime;
                float lerpFactor = (segmentDuration > 0f) ? 1f - (overshoot / segmentDuration) : 0f;

                // Interpolate between current and prev
                position = Vector3.Lerp(current.Position, prev.Position, lerpFactor);
                bladeDirection = Vector3.Lerp(current.BladeDirection, prev.BladeDirection, lerpFactor).normalized;
                tipVelocity = Vector3.Lerp(
                    GetTipVel(current),
                    GetTipVel(prev),
                    lerpFactor
                );
                return;
            }

            idx = prevIdx;
            current = prev;
        }

        // If requested timeAgo is older than our buffer, return the oldest element
        position = current.Position;
        bladeDirection = current.BladeDirection;
        tipVelocity = GetTipVel(current);
    }

    public float GetBladeTipSpeed()
    {
        if (m_elementCount < 2)
            return 0f;

        int pointsToUse = Mathf.Min(4, m_elementCount); // use up to the last 4 points
        float totalDistance = 0f;
        float totalTime = 0f;

        // start from the most recent
        int idx = (m_nextAddIndex - 1 + m_sweepData.Length) % m_sweepData.Length;

        for (int i = 0; i < pointsToUse - 1; i++)
        {
            int prevIdx = (idx - 1 + m_sweepData.Length) % m_sweepData.Length;

            Vector3 p0 = m_sweepData[idx].Position + m_sweepData[idx].BladeDirection;
            Vector3 p1 = m_sweepData[prevIdx].Position + m_sweepData[prevIdx].BladeDirection;
            float dt = m_sweepData[idx].ElapsedTime;

            // safeguard against zero or negative delta time
            if (dt > 0f)
            {
                totalDistance += Vector3.Distance(p0, p1);
                totalTime += dt;
            }

            idx = prevIdx;
        }

        return Mathf.Sqrt(totalTime > 0.001f ? totalDistance / totalTime : 0f);
    }
}