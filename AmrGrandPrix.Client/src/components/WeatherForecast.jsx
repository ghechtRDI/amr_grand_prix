import { useState, useEffect } from 'react'

function WeatherForecast() {
  const [forecast, setForecast] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  useEffect(() => {
    fetchWeatherForecast()
  }, [])

  const fetchWeatherForecast = async () => {
    try {
      setLoading(true)
      const response = await fetch('/weatherforecast')
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`)
      }
      const data = await response.json()
      setForecast(data)
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  if (loading) return <div>Loading weather forecast...</div>
  if (error) return <div>Error loading weather forecast: {error}</div>

  return (
    <div style={{ padding: '20px' }}>
      <h2>Weather Forecast</h2>
      <div style={{ display: 'grid', gap: '10px' }}>
        {forecast.map((day, index) => (
          <div
            key={index}
            style={{
              border: '1px solid #ccc',
              borderRadius: '8px',
              padding: '15px',
              backgroundColor: '#f9f9f9'
            }}
          >
            <div style={{ fontWeight: 'bold', marginBottom: '5px' }}>
              {new Date(day.date).toLocaleDateString()}
            </div>
            <div style={{ fontSize: '18px', margin: '5px 0' }}>
              {day.temperatureC}°C ({Math.round(day.temperatureC * 9/5 + 32)}°F)
            </div>
            <div style={{ fontStyle: 'italic', color: '#666' }}>
              {day.summary}
            </div>
          </div>
        ))}
      </div>
      <button
        onClick={fetchWeatherForecast}
        style={{
          marginTop: '20px',
          padding: '10px 20px',
          backgroundColor: '#007bff',
          color: 'white',
          border: 'none',
          borderRadius: '4px',
          cursor: 'pointer'
        }}
      >
        Refresh Forecast
      </button>
    </div>
  )
}

export default WeatherForecast