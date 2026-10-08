import {createTheme, MantineProvider, Button, type MantineColorsTuple} from '@mantine/core';
import {MotionConfig} from 'motion/react';
import type {ReactNode} from 'react';
import '@mantine/core/styles.css';

const marca: MantineColorsTuple=['#f4f4fc','#e9eafa','#d4d8f2','#b8c1e9','#97a5df','#7187d4','#2848ce','#1d38ac','#233b87','#26355f'];
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
