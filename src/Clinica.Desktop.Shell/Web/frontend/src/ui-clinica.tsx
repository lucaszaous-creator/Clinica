import {createTheme, MantineProvider, Button, type MantineColorsTuple} from '@mantine/core';
import {MotionConfig} from 'motion/react';
import type {ReactNode} from 'react';
import '@mantine/core/styles.css';

// Degraus existentes em Styles/Tokens.xaml, sem interpolar novos tons.
const marca: MantineColorsTuple=['#EEF3FC','#D8E3F7','#B0D3F3','#B0D3F3','#5E87D9','#3F62C9','#123A9E','#0A2E86','#07329A','#071F5C'];
const tema=createTheme({
 primaryColor:'marca',primaryShade:6,colors:{marca},fontFamily:'Inter, sans-serif',
 defaultRadius:'md',fontSizes:{xs:'12px',sm:'13px',md:'14px',lg:'16px',xl:'20px'},
 headings:{fontFamily:'Inter, sans-serif',fontWeight:'500'},
 components:{Button:Button.extend({defaultProps:{size:'sm',radius:'md'},styles:{label:{whiteSpace:'normal',textAlign:'center'},root:{height:'auto',minHeight:36}}})}
});

/** Uma identidade para os cinco executáveis, sem estado de negócio no tema. */
export function ProvedorClinica({children}:{children:ReactNode}){
 return <MantineProvider theme={tema} forceColorScheme="light"><MotionConfig reducedMotion="user">{children}</MotionConfig></MantineProvider>;
}
