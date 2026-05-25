import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useRegister } from '../services/auth';
import { useToast } from '../components/toaster';
import { Button, Card, Field, Input } from '../components/ui';
import AuthBrandPanel from '../components/AuthBrandPanel';

export default function Register() {
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const register = useRegister();
  const toast = useToast();

  const mismatched = confirm.length > 0 && password !== confirm;

  return (
    <div className="grid min-h-screen bg-surface lg:grid-cols-[minmax(0,1fr)_480px]">
      <AuthBrandPanel imageSrc="/brand/collectify-banner-dark.png" />
      <div className="flex items-center justify-center p-4 sm:p-8 lg:bg-surface">
      <Card className="w-full max-w-md !p-6 sm:!p-8">
        <div className="mb-6 flex items-center gap-3">
          <img src="/brand/collectify-logo.png" alt="" className="h-12 w-12 rounded-2xl shadow-sm" />
          <div>
            <div className="text-lg font-extrabold tracking-tight text-text-primary">Collectify</div>
            <p className="text-sm text-text-secondary">Self-hosted media tracking.</p>
          </div>
        </div>
        <h1 className="mb-6 text-2xl font-extrabold tracking-tight text-text-primary">Create an account</h1>
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault();
            if (mismatched) return;
            register.mutate(
              { userName, password },
              {
                onSuccess: () => toast.success(`Welcome, ${userName}.`),
              },
            );
          }}
        >
          <Field label="Username">
            <Input value={userName} onChange={(e) => setUserName(e.target.value)} autoFocus required />
          </Field>
          <Field label="Password">
            <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required minLength={8} />
          </Field>
          <Field label="Confirm password">
            <Input
              type="password"
              value={confirm}
              onChange={(e) => setConfirm(e.target.value)}
              required
              minLength={8}
              aria-invalid={mismatched || undefined}
            />
          </Field>
          {mismatched && <p className="text-sm text-error">Passwords don't match.</p>}
          {register.error && (
            <p className="text-sm text-error">{(register.error as Error).message ?? 'Registration failed.'}</p>
          )}
          <Button type="submit" disabled={register.isPending || mismatched} className="w-full">
            {register.isPending ? 'Creating account…' : 'Create account'}
          </Button>
        </form>
        <p className="mt-4 text-sm text-text-secondary text-center">
          Already have an account?{' '}
          <Link to="/login" className="text-brand hover:text-brand-hover underline transition-colors">
            Sign in
          </Link>
        </p>
      </Card>
      </div>
    </div>
  );
}
