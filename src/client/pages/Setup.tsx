import { useState } from 'react';
import { useSetup } from '../services/auth';
import { Button, Card, Field, Input } from '../components/ui';

export default function Setup() {
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const setup = useSetup();
  const mismatch = password.length > 0 && confirm.length > 0 && password !== confirm;

  return (
    <div className="min-h-screen flex items-center justify-center p-4">
      <Card className="w-full max-w-md">
        <h1 className="text-2xl font-semibold text-white mb-1">Welcome to Collectify</h1>
        <p className="text-slate-400 mb-6">Create your single-user account to get started.</p>
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault();
            if (!mismatch) setup.mutate({ userName, password });
          }}
        >
          <Field label="Username">
            <Input value={userName} onChange={(e) => setUserName(e.target.value)} autoFocus required minLength={1} />
          </Field>
          <Field label="Password">
            <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required minLength={8} />
          </Field>
          <Field label="Confirm password">
            <Input type="password" value={confirm} onChange={(e) => setConfirm(e.target.value)} required />
          </Field>
          {mismatch && <p className="text-sm text-rose-400">Passwords do not match.</p>}
          {setup.error && <p className="text-sm text-rose-400">{(setup.error as Error).message}</p>}
          <Button type="submit" disabled={setup.isPending || mismatch} className="w-full">
            {setup.isPending ? 'Creating account…' : 'Create account'}
          </Button>
        </form>
      </Card>
    </div>
  );
}
