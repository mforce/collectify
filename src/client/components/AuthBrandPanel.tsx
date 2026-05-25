export default function AuthBrandPanel({ imageSrc = '/brand/collectify-sample.png' }: { imageSrc?: string }) {
  return (
    <aside className="hidden min-h-screen items-center justify-center border-r border-border bg-card px-8 py-10 lg:flex">
      <div className="w-full max-w-4xl">
        <img
          src={imageSrc}
          alt=""
          className="w-full rounded-2xl border border-border bg-white object-contain shadow-card"
        />
      </div>
    </aside>
  );
}
