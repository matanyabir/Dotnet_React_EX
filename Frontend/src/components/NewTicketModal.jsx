import React, { useState } from 'react'
import { ticketsApi } from '../services/api'
import './NewTicketModal.css'

const NewTicketModal = ({ onClose, onSuccess }) => {
  const [formData, setFormData] = useState({
    fullName: '',
    email: '',
    description: ''
  })
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState(false)

  const handleChange = (e) => {
    setFormData({
      ...formData,
      [e.target.name]: e.target.value
    })
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    setError('')
    setLoading(true)

    try {
      await ticketsApi.create(formData)
      setSuccess(true)
      setTimeout(() => {
        onSuccess()
      }, 1500)
    } catch (err) {
      setError(err.response?.data?.message || 'שגיאה ביצירת הכרטיס')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2>יצירת כרטיס תמיכה חדש</h2>
          <button className="close-btn" onClick={onClose}>×</button>
        </div>

        {success ? (
          <div className="success-message">
            <h3>✓ הכרטיס נוצר בהצלחה!</h3>
            <p>אימייל נשלח ללקוח עם קישור למעקב</p>
          </div>
        ) : (
          <form onSubmit={handleSubmit}>
            {error && <div className="error-message">{error}</div>}

            <div className="form-group">
              <label>שם מלא *</label>
              <input
                type="text"
                name="fullName"
                value={formData.fullName}
                onChange={handleChange}
                required
                placeholder="הכנס שם מלא"
              />
            </div>

            <div className="form-group">
              <label>כתובת אימייל *</label>
              <input
                type="email"
                name="email"
                value={formData.email}
                onChange={handleChange}
                required
                placeholder="example@email.com"
              />
            </div>

            <div className="form-group">
              <label>תיאור הבעיה *</label>
              <textarea
                name="description"
                value={formData.description}
                onChange={handleChange}
                required
                placeholder="תאר את הבעיה בפירוט..."
                rows="6"
              />
            </div>

            <div className="modal-footer">
              <button 
                type="submit" 
                className="btn btn-primary"
                disabled={loading}
              >
                {loading ? 'יוצר...' : 'צור כרטיס'}
              </button>
              <button 
                type="button" 
                className="btn btn-secondary"
                onClick={onClose}
              >
                ביטול
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  )
}

export default NewTicketModal

