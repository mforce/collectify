import type { ReactNode } from 'react';
import { Card } from './ui';
import MediaIcon from './MediaIcon';
import { MEDIA } from '../services/mediaRegistry';
import type { MediaType } from '../services/types';
export function formatPrice(p?:number|null,c?:string|null){if(p==null)return '';const s=c==='USD'?'$':c==='EUR'?'€':c==='GBP'?'£':`${c??''} `;return `${s}${Number(p).toFixed(2)}`}
export function formatDate(d?:string|null){return d?new Date(`${d}T00:00:00`).toLocaleDateString(undefined,{year:'numeric',month:'short',day:'numeric'}):''}
export function InfoRow({label,value}:{label:string;value?:string|null}){return value?<div className="flex gap-1 py-1.5"><dt className="w-36 text-sm text-text-secondary">{label}</dt><dd>{value}</dd></div>:null}
export const detailTheme=(t:MediaType)=>({title:MEDIA[t].theme.textAccent,accent:MEDIA[t].theme.navActiveMobile,button:`border-${t}-border ${MEDIA[t].theme.textAccent} hover:bg-${t}-light`});
export function ThemedCard({children,className=''}:{type:MediaType;children:ReactNode;className?:string}){return <Card className={className}>{children}</Card>}
export function HeroTitle({type,title,subtitle}:{type:MediaType;title:string;subtitle?:ReactNode}){return <div className="flex gap-3"><MediaIcon type={type} className="h-7 w-7"/><div><h2 className={`text-2xl font-extrabold ${detailTheme(type).title}`}>{title}</h2>{subtitle&&<p className="text-sm text-text-secondary">{subtitle}</p>}</div></div>}
