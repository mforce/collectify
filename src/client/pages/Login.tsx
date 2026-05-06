import { useState } from 'react';
import { useLogin } from '../api/auth';
import { Button, Card, Field, Input } from '../components/ui';

export default function Login() {
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const login = useLogin();

  return (
    <div className="min-h-screen flex items-center justify-center p-4">
      <Card className="w-full max-w-md">
        <h1 className="text-2xl font-semibold text-white mb-6">Sign in to Collectify</h1>
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault();
            login.mutate({ userName, password });
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
      </Card>
    </div>
  );
}
