import React from 'react';
import {SearchInput} from '../core/SearchInput.jsx';
import {IconButton} from '../core/IconButton.jsx';
import {Avatar} from '../data/Avatar.jsx';
export function Topbar({left,searchPlaceholder='Pesquisar…',onSearch,notifications,user,style}){
  return <header style={{height:'var(--topbar-height)',flexShrink:0,display:'flex',alignItems:'center',gap:16,
    padding:'0 24px',background:'var(--surface-card)',borderBottom:'1px solid var(--border)',boxSizing:'border-box',...style}}>
    <div style={{flex:1,minWidth:0}}>{left}</div>
    <SearchInput pill width={260} placeholder={searchPlaceholder} onChange={onSearch}/>
    <div style={{position:'relative',display:'inline-flex'}}>
      <IconButton icon="bell" label="Notificações"/>
      {notifications?<span style={{position:'absolute',top:4,right:4,minWidth:16,height:16,padding:'0 4px',
        borderRadius:999,background:'var(--danger)',color:'#fff',fontSize:10,fontWeight:700,fontFamily:'var(--font-ui)',
        display:'flex',alignItems:'center',justifyContent:'center',boxSizing:'border-box'}}>{notifications}</span>:null}
    </div>
    {user?<Avatar name={user} size={32}/>:null}
  </header>;
}
