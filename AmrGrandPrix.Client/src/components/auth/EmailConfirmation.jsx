/**
 * EmailConfirmation Component
 * Email verification success/error page
 */

import { useState, useEffect } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import * as authService from '../../services/authService';
import './auth.css';

export const EmailConfirmation = () => {
  const [searchParams] = useSearchParams();
  const [status, setStatus] = useState('loading'); // loading, success, error
  const [message, setMessage] = useState('');

  useEffect(() => {
    const confirmEmail = async () => {
      const userId = searchParams.get('userId');
      const token = searchParams.get('token');

      if (!userId || !token) {
        setStatus('error');
        setMessage('Invalid confirmation link. Please check your email and try again.');
        return;
      }

      try {
        await authService.confirmEmail(userId, token);
        setStatus('success');
        setMessage('Your email has been confirmed successfully! You can now login to your account.');
      } catch (error) {
        setStatus('error');
        setMessage(error.message || 'Email confirmation failed. The link may have expired or is invalid.');
      }
    };

    confirmEmail();
  }, [searchParams]);

  return (
    <div className="auth-container">
      <div className="auth-card">
        {status === 'loading' && (
          <>
            <h1>Confirming Email...</h1>
            <p>Please wait while we confirm your email address.</p>
          </>
        )}

        {status === 'success' && (
          <>
            <h1>Email Confirmed!</h1>
            <div className="success-message">
              <p>{message}</p>
            </div>
            <div className="auth-links">
              <Link to="/login" className="btn-primary">
                Go to Login
              </Link>
            </div>
          </>
        )}

        {status === 'error' && (
          <>
            <h1>Confirmation Failed</h1>
            <div className="error-message">
              <p>{message}</p>
            </div>
            <div className="auth-links">
              <Link to="/register">Register Again</Link>
              {' or '}
              <Link to="/login">Login</Link>
            </div>
          </>
        )}
      </div>
    </div>
  );
};

export default EmailConfirmation;
