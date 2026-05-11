import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth, useLogin } from '../services/auth';
import { useToast } from '../components/toaster';
import { Button, Card, Field, Input } from '../components/ui';

export default function Login() {
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const login = useLogin();
  const { data: auth } = useAuth();
  const toast = useToast();

  return (
    <div className="min-h-screen flex items-center justify-center p-4">
      <Card className="w-full max-w-md">
        <h1 className="text-2xl font-semibold text-white mb-6">Sign in to Collectify</h1>
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault();
            login.mutate(
              { userName, password },
              {
                onSuccess: () => toast.success(`Welcome back, ${userName}.`),
              },
            );
          }}
        >
          <Field label="Username">
            <Input value={userName} onChange={(e) => setUserName(e.target.value)} autoFocus required />
          </Field>
          <Field label="Password">
            <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
          </Field>
          {login.error && <p className="text-sm text-rose-400">Invalid credentials.</p>}
          <Button type="submit" disabled={login.isPending} className="w-full">
            {login.isPending ? 'Signing in…' : 'Sign in'}
          </Button>
        </form>
        {auth?.allowRegistration && (
          <p className="mt-4 text-sm text-slate-400 text-center">
            Don't have an account?{' '}
            <Link to="/register" className="text-indigo-300 hover:text-indigo-200 underline">
              Create one
            </Link>
          </p>
        )}
      </Card>
    </div>
  );
}
