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
    <div className="grid min-h-screen bg-surface lg:grid-cols-[1fr_440px]">
      <div className="relative hidden overflow-hidden bg-[#071333] lg:block">
        <img
          src="/brand/collectify-sample.png"
          alt=""
          className="absolute inset-0 h-full w-full object-cover object-center"
        />
      </div>
      <div className="flex items-center justify-center p-4 sm:p-8">
      <Card className="w-full max-w-md !p-6 sm:!p-8">
        <div className="mb-6 flex items-center gap-3">
          <img src="/brand/collectify-logo.png" alt="" className="h-12 w-12 rounded-2xl shadow-sm" />
          <div>
            <div className="text-lg font-extrabold tracking-tight text-text-primary">Collectify</div>
            <p className="text-sm text-text-secondary">Your server. Your collection.</p>
          </div>
        </div>
        <h1 className="mb-6 text-2xl font-extrabold tracking-tight text-text-primary">Sign in</h1>
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
          {login.error && <p className="text-sm text-error">Invalid credentials.</p>}
          <Button type="submit" disabled={login.isPending} className="w-full">
            {login.isPending ? 'Signing in…' : 'Sign in'}
          </Button>
        </form>
        {auth?.allowRegistration && (
          <p className="mt-4 text-sm text-text-secondary text-center">
            Don't have an account?{' '}
            <Link to="/register" className="text-brand hover:text-brand-hover underline transition-colors">
              Create one
            </Link>
          </p>
        )}
      </Card>
      </div>
    </div>
  );
}
