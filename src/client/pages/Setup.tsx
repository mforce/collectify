import { useState } from 'react';
import { useSetup } from '../services/auth';
import { useToast } from '../components/toaster';
import { Button, Card, Field, Input } from '../components/ui';
import AuthBrandPanel from '../components/AuthBrandPanel';

export default function Setup() {
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const setup = useSetup();
  const toast = useToast();
  const mismatch = password.length > 0 && confirm.length > 0 && password !== confirm;

  return (
    <div className="grid min-h-screen bg-surface lg:grid-cols-[minmax(0,1fr)_480px]">
      <AuthBrandPanel imageSrc="/brand/collectify-banner-light.png" />
      <div className="flex items-center justify-center p-4 sm:p-8 lg:bg-surface">
      <Card className="w-full max-w-md !p-6 sm:!p-8">
        <div className="mb-6 flex items-center gap-3">
          <img src="/brand/collectify-logo.png" alt="" className="h-12 w-12 rounded-2xl shadow-sm" />
          <div>
            <div className="text-lg font-extrabold tracking-tight text-text-primary">Collectify</div>
            <p className="text-sm text-text-secondary">Private by default.</p>
          </div>
        </div>
        <h1 className="mb-1 text-2xl font-extrabold tracking-tight text-text-primary">Welcome to Collectify</h1>
        <p className="text-text-secondary mb-6">Create your single-user account to get started.</p>
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault();
            if (!mismatch)
              setup.mutate(
                { userName, password },
                { onSuccess: () => toast.success('Account created.') },
              );
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
          {mismatch && <p className="text-sm text-error">Passwords do not match.</p>}
          {setup.error && <p className="text-sm text-error">{(setup.error as Error).message}</p>}
          <Button type="submit" disabled={setup.isPending || mismatch} className="w-full">
            {setup.isPending ? 'Creating account…' : 'Create account'}
          </Button>
        </form>
      </Card>
      </div>
    </div>
  );
}
